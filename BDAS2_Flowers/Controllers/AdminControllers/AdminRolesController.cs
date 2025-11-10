using BDAS2_Flowers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers.AdminControllers;

[Authorize(Roles = "Admin")]
[Route("admin/roles")]
public class AdminRolesController : Controller
{
    private readonly IDbFactory _db;
    public AdminRolesController(IDbFactory db) => _db = db;

    // GET /admin/roles?q=Admin
    [HttpGet("")]
    public async Task<IActionResult> Index(string? q)
    {
        var rows = new List<(int Id, string Name, int Users)>();
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            SELECT ID, NAME, USER_COUNT
              FROM VW_ADMIN_ROLES
             WHERE :q IS NULL
                OR :q = ''
                OR UPPER(NAME) LIKE UPPER('%' || :q || '%')
             ORDER BY NAME";
        var p = cmd.CreateParameter();
        p.ParameterName = "q";
        p.Value = (object?)q ?? DBNull.Value;
        cmd.Parameters.Add(p);

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            rows.Add((r.GetInt32(0), r.GetString(1), r.GetInt32(2)));

        ViewBag.Query = q ?? "";
        ViewData["Title"] = "Role";
        return View("/Views/AdminPanel/Roles/Index.cshtml", rows);
    }

    // POST /admin/roles/create
    [ValidateAntiForgeryToken]
    [HttpPost("create")]
    public async Task<IActionResult> Create(string name)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_ROLE_CREATE", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_name", OracleDbType.Varchar2, 30).Value = name?.Trim();
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";

        try { await cmd.ExecuteNonQueryAsync(); TempData["Msg"] = "Role vytvořena."; }
        catch (OracleException ex)
        {
            var nice = ex.Number switch
            {
                20101 => "Název role je povinný.",
                20102 => "Role stejného názvu již existuje.",
                _ => ex.Message
            };
            TempData["Msg"] = "Nelze vytvořit roli: " + nice;
        }
        return RedirectToAction(nameof(Index));
    }

    // POST /admin/roles/{id}/rename
    [ValidateAntiForgeryToken]
    [HttpPost("{id:int}/rename")]
    public async Task<IActionResult> Rename(int id, string name)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_ROLE_RENAME", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_name", OracleDbType.Varchar2, 30).Value = name?.Trim();
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";

        try { await cmd.ExecuteNonQueryAsync(); TempData["Msg"] = "Role přejmenována."; }
        catch (OracleException ex)
        {
            var nice = ex.Number switch
            {
                20101 => "Název role je povinný.",
                20102 => "Role stejného názvu již existuje.",
                20104 => "Role nenalezena.",
                _ => ex.Message
            };
            TempData["Msg"] = "Nelze přejmenovat roli: " + nice;
        }
        return RedirectToAction(nameof(Index));
    }

    // POST /admin/roles/{id}/delete
    [ValidateAntiForgeryToken]
    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_ROLE_DELETE", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";

        try { await cmd.ExecuteNonQueryAsync(); TempData["Msg"] = "Role odstraněna."; }
        catch (OracleException ex)
        {
            var nice = ex.Number switch
            {
                20105 => "Nelze smazat: roli používají uživatelé.",
                20104 => "Role nenalezena.",
                _ => ex.Message
            };
            TempData["Msg"] = "Nelze odstranit roli: " + nice;
        }
        return RedirectToAction(nameof(Index));
    }
}