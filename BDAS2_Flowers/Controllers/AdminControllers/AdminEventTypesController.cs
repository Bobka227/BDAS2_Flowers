using BDAS2_Flowers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers.AdminControllers;

/// <summary>
/// Administrátorský controller pro správu typů událostí.
/// Umožňuje typy událostí zobrazit, vytvářet, upravovat a mazat.
/// </summary>
[Authorize(Roles = "Admin")]
[Route("admin/event-types")]
public class AdminEventTypesController : Controller
{
    private readonly IDbFactory _db;

    /// <summary>
    /// Inicializuje novou instanci <see cref="AdminEventTypesController"/> s továrnou databázových připojení.
    /// </summary>
    /// <param name="db">Továrna pro vytváření a otevírání databázových připojení.</param>
    public AdminEventTypesController(IDbFactory db) => _db = db;

    /// <summary>
    /// Zobrazí seznam všech typů událostí.
    /// </summary>
    /// <returns>
    /// View s kolekcí řádků (Id, název, popis) pro jednotlivé typy událostí.
    /// </returns>
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var rows = new List<(int Id, string Name, string? Description)>();
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = con.CreateCommand();
        cmd.CommandText = @"SELECT ID, NAME, DESCRIPTION FROM VW_EVENT_TYPES ORDER BY NAME";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            rows.Add((r.GetInt32(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2)));
        ViewData["Title"] = "Typy událostí";
        return View("/Views/AdminPanel/EventTypes/Index.cshtml", rows);
    }

    /// <summary>
    /// Vytvoří nový typ události pomocí uložené procedury <c>PRC_EVENT_TYPE_CREATE</c>.
    /// </summary>
    /// <param name="name">Název nového typu události.</param>
    /// <param name="description">Popis typu události (nepovinný).</param>
    /// <returns>Přesměrování zpět na seznam typů událostí.</returns>
    [ValidateAntiForgeryToken]
    [HttpPost("create")]
    public async Task<IActionResult> Create(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Err"] = "Název je povinný.";
            return RedirectToAction(nameof(Index));
        }
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_EVENT_TYPE_CREATE", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_name", OracleDbType.Varchar2, 100).Value = name.Trim();
        cmd.Parameters.Add("p_desc", OracleDbType.Varchar2, 2000).Value =
            (object?)(description?.Trim()) ?? DBNull.Value;
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";
        try { await cmd.ExecuteNonQueryAsync(); TempData["Msg"] = "Typ vytvořen."; }
        catch (OracleException ex) { TempData["Err"] = "Nelze vytvořit typ: " + ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Upraví existující typ události (název a popis) pomocí uložené procedury <c>PRC_EVENT_TYPE_RENAME</c>.
    /// </summary>
    /// <param name="id">Identifikátor upravovaného typu události.</param>
    /// <param name="name">Nový název typu události.</param>
    /// <param name="description">Nový popis typu události (nepovinný).</param>
    /// <returns>Přesměrování zpět na seznam typů událostí.</returns>
    [ValidateAntiForgeryToken]
    [HttpPost("rename")]
    public async Task<IActionResult> Rename(int id, string name, string? description)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_EVENT_TYPE_RENAME", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_name", OracleDbType.Varchar2, 100).Value = name.Trim();
        cmd.Parameters.Add("p_desc", OracleDbType.Varchar2, 2000).Value =
            (object?)(description?.Trim()) ?? DBNull.Value;
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";
        try { await cmd.ExecuteNonQueryAsync(); TempData["Msg"] = "Typ upraven."; }
        catch (OracleException ex) { TempData["Err"] = "Nelze upravit: " + ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Smaže existující typ události pomocí uložené procedury <c>PRC_EVENT_TYPE_DELETE</c>.
    /// </summary>
    /// <param name="id">Identifikátor mazáného typu události.</param>
    /// <returns>Přesměrování zpět na seznam typů událostí s výslednou zprávou.</returns>
    [ValidateAntiForgeryToken]
    [HttpPost("delete")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_EVENT_TYPE_DELETE", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";
        try { await cmd.ExecuteNonQueryAsync(); TempData["Msg"] = "Typ smazán."; }
        catch (OracleException ex) { TempData["Err"] = "Nelze smazat typ (pravděpodobně používán): " + ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}
