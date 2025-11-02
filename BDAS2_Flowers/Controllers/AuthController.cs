using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels;
using BDAS2_Flowers.Security;

namespace BDAS2_Flowers.Controllers;

public class AuthController : Controller
{
    private readonly IDbFactory _db;
    private readonly IPasswordHasher _hasher;

    public AuthController(IDbFactory db, IPasswordHasher hasher)
    { _db = db; _hasher = hasher; }

    [HttpGet("/auth/register")]
    public IActionResult Register() => View(new RegisterVm());

    [ValidateAntiForgeryToken]
    [HttpPost("/auth/register")]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        await using var conn = await _db.CreateOpenAsync();

        await using (var c = conn.CreateCommand())
        {
            c.CommandText = "SELECT COUNT(*) FROM \"USER\" WHERE email = :email";
            c.Parameters.Add(new OracleParameter("email", OracleDbType.Varchar2, vm.Email.ToLower(), ParameterDirection.Input));
            var exists = Convert.ToInt32(await c.ExecuteScalarAsync()) > 0;
            if (exists)
            {
                ModelState.AddModelError(nameof(vm.Email), "Tento e-mail už je registrován.");
                return View(vm);
            }
        }

        int newId;
        await using (var c = conn.CreateCommand())
        {
            c.CommandText = "SELECT NVL(MAX(userid),0)+1 FROM \"USER\"";
            newId = Convert.ToInt32(await c.ExecuteScalarAsync());
        }

        var hash = _hasher.Hash(vm.Password);
        await using (var c = conn.CreateCommand())
        {
            c.CommandText = @"
              INSERT INTO ""USER"" (userid, email, passwordhash, createdat, roleid, firstname, lastname, phone)
              VALUES (:id, :email, :phash, SYSDATE, :role, :fn, :ln, :phone)";
            c.Parameters.Add(new OracleParameter("id", OracleDbType.Int32, newId, ParameterDirection.Input));
            c.Parameters.Add(new OracleParameter("email", OracleDbType.Varchar2, vm.Email.ToLower(), ParameterDirection.Input));
            c.Parameters.Add(new OracleParameter("phash", OracleDbType.Varchar2, hash, ParameterDirection.Input));
            c.Parameters.Add(new OracleParameter("role", OracleDbType.Int32, 1, ParameterDirection.Input));
            c.Parameters.Add(new OracleParameter("fn", OracleDbType.Varchar2, vm.FirstName, ParameterDirection.Input));
            c.Parameters.Add(new OracleParameter("ln", OracleDbType.Varchar2, vm.LastName, ParameterDirection.Input));
            c.Parameters.Add(new OracleParameter("phone", OracleDbType.Int32, int.Parse(vm.Phone), ParameterDirection.Input));

            await c.ExecuteNonQueryAsync();
        }

        await SignInAsync(newId, vm.Email.ToLower(), "Customer", vm.FirstName, vm.LastName);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("/auth/login")]
    public IActionResult Login(string? returnUrl = null) => View(new LoginVm { ReturnUrl = returnUrl });

    [ValidateAntiForgeryToken]
    [HttpPost("/auth/login")]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        await using var conn = await _db.CreateOpenAsync();

        int? id = null; string? email = null; string? stored = null;
        string role = "Customer"; string? fn = null, ln = null;

        await using (var c = conn.CreateCommand())
        {
            c.CommandText = @"
          SELECT u.userid, u.email, u.passwordhash, r.rolename, u.firstname, u.lastname
          FROM ""USER"" u
          JOIN role r ON r.roleid = u.roleid
          WHERE LOWER(u.email) = :email";
            c.Parameters.Add(new OracleParameter("email", OracleDbType.Varchar2, vm.Email.ToLower(), ParameterDirection.Input));

            await using var r = await c.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            if (await r.ReadAsync())
            {
                id = DbRead.GetInt32(r, 0);
                email = r.GetString(1);
                stored = r.GetString(2);
                role = r.GetString(3);
                fn = r.GetString(4);
                ln = r.GetString(5);
            }
        }

        if (id is null)
        {
            ModelState.AddModelError(nameof(vm.Email), "Účet s tímto e-mailem neexistuje.");
            ViewBag.ShowRegisterPrompt = true;
            return View(vm);
        }

        if (!_hasher.Verify(vm.Password, stored!))
        {
            ModelState.AddModelError(nameof(vm.Password), "Nesprávný e-mail nebo heslo.");
            return View(vm);
        }

        await SignInAsync(id.Value, email!, role, fn!, ln!);

        if (!string.IsNullOrWhiteSpace(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
            return Redirect(vm.ReturnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpPost("/auth/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("/auth/denied")]
    public IActionResult Denied() => Content("Přístup odepřen.");

    private async Task SignInAsync(int userId, string email, string role, string firstName, string lastName)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, $"{firstName} {lastName}"),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role)
        };
        var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(id));
    }
}
