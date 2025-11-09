using BDAS2_Flowers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers.AdminControllers;

[Authorize(Roles = "Admin")]
[Route("admin/delivery-methods")]
public class AdminDeliveryMethodsController : Controller
{
    private readonly IDbFactory _db;
    public AdminDeliveryMethodsController(IDbFactory db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var rows = new List<(int Id, string Name)>();
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = con.CreateCommand();
        cmd.CommandText = @"SELECT ID, NAME FROM VW_DELIVERY_METHODS ORDER BY NAME";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            rows.Add((r.GetInt32(0), r.GetString(1)));

        ViewData["Title"] = "Způsoby doručení";
        return View("/Views/AdminPanel/DeliveryMethods/Index.cshtml", rows);
    }

    [ValidateAntiForgeryToken]
    [HttpPost("create")]
    public async Task<IActionResult> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Msg"] = "Název je povinný.";
            return RedirectToAction(nameof(Index));
        }

        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_DELIVERY_METHOD_CREATE", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_name", OracleDbType.Varchar2, 200).Value = name.Trim();
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";

        try { await cmd.ExecuteNonQueryAsync(); TempData["Msg"] = "Způsob doručení vytvořen."; }
        catch (OracleException ex) { TempData["Msg"] = "Nelze vytvořit: " + ex.Message; }

        return RedirectToAction(nameof(Index));
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{id:int}/rename")]
    public async Task<IActionResult> Rename(int id, string name)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_DELIVERY_METHOD_RENAME", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_name", OracleDbType.Varchar2, 200).Value = name?.Trim();
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";

        try { await cmd.ExecuteNonQueryAsync(); TempData["Msg"] = "Přejmenováno."; }
        catch (OracleException ex) { TempData["Msg"] = "Nelze přejmenovat: " + ex.Message; }

        return RedirectToAction(nameof(Index));
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_DELIVERY_METHOD_DELETE", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";

        try { await cmd.ExecuteNonQueryAsync(); TempData["Msg"] = "Odstraněno."; }
        catch (OracleException ex) { TempData["Msg"] = "Nelze odstranit (používáno objednávkami): " + ex.Message; }

        return RedirectToAction(nameof(Index));
    }
}
