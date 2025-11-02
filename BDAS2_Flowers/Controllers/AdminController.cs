using BDAS2_Flowers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers;

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
        cmd.CommandText = @"SELECT u.userid, u.email, u.firstname||' '||u.lastname AS name, r.rolename
                            FROM ""USER"" u JOIN role r ON r.roleid=u.roleid
                            ORDER BY u.userid";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            rows.Add((DbRead.GetInt32(r, 0), r.GetString(1), r.GetString(2), r.GetString(3)));
        return View(rows);
    }

    [ValidateAntiForgeryToken]
    [HttpPost("users/{id:int}/promote")]
    public async Task<IActionResult> Promote(int id)
    {
        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"UPDATE ""USER"" SET roleid = 2 WHERE userid = :id";
        cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Int32, id, ParameterDirection.Input));
        var n = await cmd.ExecuteNonQueryAsync();
        TempData["Msg"] = n == 1 ? "Role changed to Admin." : "User not found.";
        return RedirectToAction(nameof(Users));
    }

    [ValidateAntiForgeryToken]
    [HttpPost("users/{id:int}/demote")]
    public async Task<IActionResult> Demote(int id)
    {
        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"UPDATE ""USER"" SET roleid = 1 WHERE userid = :id";
        cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Int32, id, ParameterDirection.Input));
        await cmd.ExecuteNonQueryAsync();
        TempData["Msg"] = "Role changed to Customer.";
        return RedirectToAction(nameof(Users));
    }
}
