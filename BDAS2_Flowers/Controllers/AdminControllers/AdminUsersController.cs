using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.AdminModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace BDAS2_Flowers.Controllers.AdminControllers;

[Authorize(Roles = "Admin")]
[Route("admin/users")]
public class AdminUsersController : Controller
{
    private readonly IDbFactory _db;
    public AdminUsersController(IDbFactory db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var rows = new List<(string Email, string Name, string Role, int Orders, string Segment)>();

        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT EMAIL, FULLNAME, ROLE_NAME, ORDER_COUNT, SEGMENT
        FROM ST72861.VW_USERS_ADMIN
        ORDER BY FULLNAME";

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add((
                Email: r.GetString(0),
                Name: r.GetString(1),
                Role: r.GetString(2),
                Orders: DbRead.GetInt32(r, 3),
                Segment: r.IsDBNull(4) ? "" : r.GetString(4)
            ));
        }

        return View("/Views/AdminPanel/Users/Users.cshtml", rows);
    }


    [ValidateAntiForgeryToken]
    [HttpPost("{email}/role")]
    public async Task<IActionResult> SetRole(string email, string roleName)
    {
        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_SET_USER_ROLE", (OracleConnection)conn)
        { CommandType = CommandType.StoredProcedure };
        cmd.BindByName = true;
        cmd.Parameters.Add("p_email", OracleDbType.Varchar2, 200).Value = email;
        cmd.Parameters.Add("p_role_name", OracleDbType.Varchar2, 50).Value = roleName;
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";
        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = $"Role changed to {roleName}.";
        }
        catch (OracleException ex)
        {
            TempData["Msg"] = "Cannot change role: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{email}/delete")]
    public async Task<IActionResult> Delete(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Msg"] = "Chybí e-mail uživatele.";
            return RedirectToAction(nameof(Index));
        }

        var currentEmail = User.FindFirstValue(ClaimTypes.Email) ?? "";

        if (string.Equals(email, currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            TempData["Msg"] = "Nemůžete smazat sami sebe.";
            return RedirectToAction(nameof(Index));
        }

        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("ST72861.PRC_USER_DELETE", (OracleConnection)conn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };

        cmd.Parameters.Add("p_email", OracleDbType.Varchar2, 200).Value = email.Trim();
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";

        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = $"Uživatel {email} byl úspěšně smazán.";
        }
        catch (OracleException ex)
        {
            switch (ex.Number)
            {
                case 20082:
                    TempData["Msg"] = $"Nelze smazat uživatele {email}: má objednávky.";
                    break;
                case 20081:
                case 20083:
                    TempData["Msg"] = $"Uživatel {email} nebyl nalezen.";
                    break;
                default:
                    TempData["Msg"] = "Chyba při mazání uživatele: " + ex.Message;
                    break;
            }
        }

        return RedirectToAction(nameof(Index));
    }


    [HttpGet("{email}/orders")]
    public async Task<IActionResult> UserOrders(string email)
    {
        var vm = new AdminUserOrdersVm { Email = email, Orders = new(), StatusNames = new() };

        await using var conn = await _db.CreateOpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT FULLNAME
                FROM VW_USERS_ADMIN
                WHERE UPPER(EMAIL) = UPPER(:e)";

            cmd.Parameters.Add(new OracleParameter("e", OracleDbType.Varchar2, email, ParameterDirection.Input));

            var nameObj = await cmd.ExecuteScalarAsync();
            if (nameObj is null)
                return NotFound();

            vm.FullName = Convert.ToString(nameObj)!;
        }


        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"SELECT NAME FROM VW_STATUSES ORDER BY NAME";
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync()) vm.StatusNames.Add(rd.GetString(0));
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
             SELECT ORDER_NO, ORDERDATE, STATUS, DELIVERY, SHOP, TOTAL
             FROM VW_USER_ORDERS
             WHERE UPPER(EMAIL)=UPPER(:e)
             ORDER BY ORDERDATE DESC";
            cmd.Parameters.Add(new OracleParameter("e", OracleDbType.Varchar2, email, ParameterDirection.Input));
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                vm.Orders.Add(new AdminOrderRowVm
                {
                    OrderNo = rd.GetString(0),
                    OrderDate = rd.GetDateTime(1),
                    Status = rd.GetString(2),
                    Delivery = rd.GetString(3),
                    Shop = rd.GetString(4),
                    Total = (decimal)rd.GetDecimal(5)
                });
            }
        }

        return View("/Views/AdminPanel/Users/UserOrders.cshtml", vm);
    }


    [HttpPost("impersonate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Impersonate(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["DiagErr"] = "Chybí e-mail uživatele.";
            return RedirectToAction("Index");
        }

        var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "";

        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.BindByName = true;

        cmd.CommandText = @"
        SELECT USERID,
               EMAIL,
               FULLNAME,
               ROLE_NAME
          FROM VW_USERS_SECURITY
         WHERE UPPER(EMAIL) = UPPER(:email)";
        cmd.Parameters.Add("email", OracleDbType.Varchar2).Value = email.Trim();

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            TempData["DiagErr"] = "Uživatel nebyl nalezen.";
            return RedirectToAction("Index");
        }

        var userId = reader.GetInt32(0);
        var userMail = reader.GetString(1);
        var fullName = reader.IsDBNull(2) ? "" : reader.GetString(2);
        var roleName = reader.GetString(3);

        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim(ClaimTypes.Name, fullName),
        new Claim(ClaimTypes.Email, userMail),
        new Claim(ClaimTypes.Role, roleName),
        new Claim("ImpersonatedBy", adminEmail)
    };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return RedirectToAction("Index", "Home");
    }



}
