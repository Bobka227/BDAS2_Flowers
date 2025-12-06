using BDAS2_Flowers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers.AdminControllers;

/// <summary>
/// Administrátorský controller pro správu stavů objednávek.
/// Umožňuje statusy vypisovat, vytvářet, přejmenovávat a mazat.
/// </summary>
[Authorize(Roles = "Admin")]
[Route("admin/statuses")]
public class AdminStatusesController : Controller
{
    private readonly IDbFactory _db;

    /// <summary>
    /// Inicializuje novou instanci <see cref="AdminStatusesController"/> s továrnou databázových připojení.
    /// </summary>
    /// <param name="db">Továrna pro vytváření a otevírání databázových připojení.</param>
    public AdminStatusesController(IDbFactory db) => _db = db;

    /// <summary>
    /// Zobrazí seznam všech statusů objednávek.
    /// </summary>
    /// <returns>
    /// View s kolekcí dvojic (Id, název statusu) načtenou z pohledu <c>VW_ADMIN_STATUSES</c>.
    /// </returns>
    // GET /admin/statuses
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var rows = new List<(int Id, string Name)>();
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = con.CreateCommand();
        cmd.CommandText = @"SELECT ID, NAME FROM VW_ADMIN_STATUSES ORDER BY NAME";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            rows.Add((r.GetInt32(0), r.GetString(1)));

        ViewData["Title"] = "Statusy";
        return View("/Views/AdminPanel/Statuses/Index.cshtml", rows);
    }

    /// <summary>
    /// Vytvoří nový status objednávky pomocí uložené procedury <c>PRC_STATUS_CREATE</c>.
    /// </summary>
    /// <param name="name">Název nového statusu.</param>
    /// <returns>
    /// Přesměrování zpět na seznam statusů s informační zprávou o úspěchu nebo chybě.
    /// </returns>
    // POST /admin/statuses/create
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
        await using var cmd = new OracleCommand("PRC_STATUS_CREATE", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure };
        cmd.BindByName = true;
        cmd.Parameters.Add("p_name", OracleDbType.Varchar2, 200).Value = name.Trim();
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";

        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = "Status vytvořen.";
        }
        catch (OracleException ex)
        {
            TempData["Msg"] = "Nelze vytvořit status: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Přejmenuje existující status objednávky pomocí uložené procedury <c>PRC_STATUS_RENAME</c>.
    /// </summary>
    /// <param name="id">Identifikátor upravovaného statusu.</param>
    /// <param name="name">Nový název statusu.</param>
    /// <returns>
    /// Přesměrování zpět na seznam statusů s informační zprávou o výsledku operace.
    /// </returns>
    // POST /admin/statuses/{id}/rename
    [ValidateAntiForgeryToken]
    [HttpPost("{id:int}/rename")]
    public async Task<IActionResult> Rename(int id, string name)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_STATUS_RENAME", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure };
        cmd.BindByName = true;
        cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_name", OracleDbType.Varchar2, 200).Value = name?.Trim();
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";

        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = "Status přejmenován.";
        }
        catch (OracleException ex)
        {
            TempData["Msg"] = "Nelze přejmenovat status: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Smaže status objednávky pomocí uložené procedury <c>PRC_STATUS_DELETE</c>.
    /// Pokud je status používán v objednávkách, bude smazání odmítnuto.
    /// </summary>
    /// <param name="id">Identifikátor mazaného statusu.</param>
    /// <returns>
    /// Přesměrování zpět na seznam statusů s informační zprávou o úspěchu nebo chybě.
    /// </returns>
    // POST /admin/statuses/{id}/delete
    [ValidateAntiForgeryToken]
    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_STATUS_DELETE", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure };
        cmd.BindByName = true;
        cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";

        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = "Status odstraněn.";
        }
        catch (OracleException ex)
        {
            TempData["Msg"] = "Nelze odstranit status (pravděpodobně používán objednávkami): " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
