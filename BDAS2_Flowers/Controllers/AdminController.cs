using BDAS2_Flowers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using BDAS2_Flowers.Models.ViewModels;

[Authorize(Roles = "Admin")]
[Route("admin")]
public class AdminController : Controller
{
    private readonly IDbFactory _db;
    public AdminController(IDbFactory db) => _db = db;

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Admin";
        return View();
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users()
    {
        var rows = new List<(int Id, string Email, string Name, string Role)>();
        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT u.userid, u.email, u.firstname||' '||u.lastname AS name, r.rolename
            FROM ""USER"" u JOIN role r ON r.roleid=u.roleid
            ORDER BY u.userid";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            rows.Add((DbRead.GetInt32(r, 0), r.GetString(1), r.GetString(2), r.GetString(3)));
        return View(rows);
    }

    [ValidateAntiForgeryToken]
    [HttpPost("users/{email}/role")]
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
        return RedirectToAction(nameof(Users));
    }


    [HttpGet("users/{email}/orders")]
    public async Task<IActionResult> UserOrders(string email)
    {
        var vm = new AdminUserOrdersVm { Email = email, Orders = new(), StatusNames = new() };

        await using var conn = await _db.CreateOpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT u.firstname||' '||u.lastname AS name
                FROM ""USER"" u WHERE UPPER(u.email)=UPPER(:e)";
            cmd.Parameters.Add(new OracleParameter("e", OracleDbType.Varchar2, email, ParameterDirection.Input));
            var nameObj = await cmd.ExecuteScalarAsync();
            if (nameObj is null) return NotFound();
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


        return View("/Views/Admin/UserOrders.cshtml", vm);
    }

    [ValidateAntiForgeryToken]
    [HttpPost("orders/{orderNo}/status")]
    public async Task<IActionResult> ChangeOrderStatus(string orderNo, string statusName, string returnEmail)
    {
        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_CHANGE_ORDER_STATUS_UI", (OracleConnection)conn)
        { CommandType = CommandType.StoredProcedure };
        cmd.BindByName = true;
        cmd.Parameters.Add("p_order_no", OracleDbType.Varchar2, 50).Value = orderNo;
        cmd.Parameters.Add("p_status_name", OracleDbType.Varchar2, 50).Value = statusName;
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";
        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = $"Order {orderNo} → {statusName}.";
        }
        catch (OracleException ex)
        {
            TempData["Msg"] = "Nelze změnit status: " + ex.Message;
        }
        return Redirect($"/admin/users/{returnEmail}/orders");
    }
}
