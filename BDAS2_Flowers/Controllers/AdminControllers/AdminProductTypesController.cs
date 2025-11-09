using BDAS2_Flowers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers.AdminControllers;

[Authorize(Roles = "Admin")]
[Route("admin/product-types")]
public class AdminProductTypesController : Controller
{
    private readonly IDbFactory _db;
    public AdminProductTypesController(IDbFactory db) => _db = db;

    // GET /admin/product-types
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var rows = new List<(int Id, string Name)>();
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = con.CreateCommand();
        cmd.CommandText = @"SELECT ID, NAME FROM VW_PRODUCT_TYPES ORDER BY NAME";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            rows.Add((r.GetInt32(0), r.GetString(1)));

        ViewData["Title"] = "Typy produktů";
        return View("/Views/AdminPanel/ProductTypes/Index.cshtml", rows);
    }

    // POST /admin/product-types/create
    [ValidateAntiForgeryToken]
    [HttpPost("create")]
    public async Task<IActionResult> Create(string name)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("ST72861.PRC_PRODUCT_TYPE_CREATE", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure };
        cmd.BindByName = true;
        cmd.Parameters.Add("p_name", OracleDbType.Varchar2, 200).Value = name?.Trim();
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";
        cmd.Parameters.Add("o_type_id", OracleDbType.Int32).Direction = ParameterDirection.Output;
        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = "Typ vytvořen.";
        }
        catch (OracleException ex)
        {
            TempData["Msg"] = "Nelze vytvořit typ: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    // POST /admin/product-types/{id}/rename
    [ValidateAntiForgeryToken]
    [HttpPost("{id:int}/rename")]
    public async Task<IActionResult> Rename(int id, string name)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("ST72861.PRC_PRODUCT_TYPE_RENAME", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure };
        cmd.BindByName = true;
        cmd.Parameters.Add("p_type_id", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_name", OracleDbType.Varchar2, 200).Value = name?.Trim();
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";
        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = "Typ přejmenován.";
        }
        catch (OracleException ex)
        {
            TempData["Msg"] = "Nelze přejmenovat: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    // POST /admin/product-types/{id}/delete
    [ValidateAntiForgeryToken]
    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("ST72861.PRC_PRODUCT_TYPE_DELETE", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure };
        cmd.BindByName = true;
        cmd.Parameters.Add("p_type_id", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";
        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = "Typ odstraněn.";
        }
        catch (OracleException ex)
        {
            TempData["Msg"] = "Nelze odstranit typ: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
