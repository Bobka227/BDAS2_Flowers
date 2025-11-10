using BDAS2_Flowers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers.AdminControllers;

[Authorize(Roles = "Admin")]
[Route("admin/shops")]
public class AdminShopsController : Controller
{
    private readonly IDbFactory _db;
    public AdminShopsController(IDbFactory db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var rows = new List<(int Id, string Name)>();
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = con.CreateCommand();
        cmd.CommandText = @"SELECT ID, NAME FROM VW_SHOPS ORDER BY NAME";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) rows.Add((r.GetInt32(0), r.GetString(1)));
        ViewData["Title"] = "Prodejny";
        return View("/Views/AdminPanel/Shops/Index.cshtml", rows);
    }

    [ValidateAntiForgeryToken]
    [HttpPost("create")]
    public async Task<IActionResult> Create(string name, string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            TempData["Err"] = "Zadejte telefon pro prodejnu.";
            return RedirectToAction(nameof(Index));
        }

        if (!long.TryParse(new string(phone.Where(char.IsDigit).ToArray()), out var phoneNum))
        {
            TempData["Err"] = "Telefon má nesprávný formát.";
            return RedirectToAction(nameof(Index));
        }

        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_SHOP_CREATE", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure, BindByName = true };

        cmd.Parameters.Add("p_name", OracleDbType.Varchar2, 50).Value = name.Trim();
        cmd.Parameters.Add("p_phone", OracleDbType.Int64).Value = phoneNum;
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";

        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = "Prodejna vytvořena.";
        }
        catch (OracleException ex)
        {
            TempData["Err"] = "Nelze vytvořit prodejnu: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [ValidateAntiForgeryToken]
    [HttpPost("rename")]
    public async Task<IActionResult> Rename(int id, string name)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_SHOP_RENAME", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure, BindByName = true };

        cmd.Parameters.Add("p_shop_id", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_name", OracleDbType.Varchar2, 50).Value = name.Trim();
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";

        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = "Prodejna přejmenována.";
        }
        catch (OracleException ex)
        {
            TempData["Err"] = "Nelze přejmenovat: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [ValidateAntiForgeryToken]
    [HttpPost("delete")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_SHOP_DELETE", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure, BindByName = true };

        cmd.Parameters.Add("p_shop_id", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";

        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = "Prodejna smazána.";
        }
        catch (OracleException ex)
        {
            TempData["Err"] = "Nelze smazat prodejnu: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
