using BDAS2_Flowers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using BDAS2_Flowers.Models.ViewModels.AdminModels;

namespace BDAS2_Flowers.Controllers.AdminControllers;

/// <summary>
/// Administrátorský controller domovské stránky admin sekce.
/// Zobrazuje diagnostické logy a umožňuje jejich mazání a reset sekvencí v databázi.
/// </summary>
[Authorize(Roles = "Admin")]
[Route("admin")]
public class AdminHomeController : Controller
{
    private readonly IDbFactory _db;

    /// <summary>
    /// Inicializuje novou instanci <see cref="AdminHomeController"/> s továrnou databázových připojení.
    /// </summary>
    /// <param name="db">Továrna pro vytváření a otevírání databázových připojení.</param>
    public AdminHomeController(IDbFactory db) => _db = db;

    /// <summary>
    /// Zobrazí hlavní stránku administrace s přehledem logů změn v databázi.
    /// </summary>
    /// <param name="page">Číslo stránky logů (výchozí 1).</param>
    /// <param name="size">Počet záznamů na stránku (výchozí 20).</param>
    /// <returns>View s modelem <see cref="LogsPageVm"/>.</returns>
    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, int size = 20)
    {
        ViewData["Title"] = "Admin";
        var logs = await LoadLogsAsync(page, size);
        return View("/Views/AdminPanel/Index.cshtml", logs);
    }

    /// <summary>
    /// Vrátí částečné view s tabulkou logů pro ajaxové načítání/přepínání stránek.
    /// </summary>
    /// <param name="page">Číslo stránky logů (výchozí 1).</param>
    /// <param name="size">Počet záznamů na stránku (výchozí 20).</param>
    /// <returns>Partial view s modelem <see cref="LogsPageVm"/>.</returns>
    [HttpGet("logs")]
    public async Task<IActionResult> Logs(int page = 1, int size = 20)
    {
        var logs = await LoadLogsAsync(page, size);
        return PartialView("/Views/AdminPanel/_LogsTable.cshtml", logs);
    }

    /// <summary>
    /// Tvrdé vyčištění logů – provede TRUNCATE tabulky logů a resetuje sekvenci
    /// pomocí procedury <c>ST72861.PKG_RESET.CLEAR_LOG</c>.
    /// </summary>
    /// <returns>Přesměrování zpět na hlavní stránku administrace s informační zprávou.</returns>
    [ValidateAntiForgeryToken]
    [HttpPost("logs/clear-hard")]
    public async Task<IActionResult> ClearLogsHard()
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("BEGIN ST72861.PKG_RESET.CLEAR_LOG; END;", (OracleConnection)con)
        { CommandType = CommandType.Text };
        await cmd.ExecuteNonQueryAsync();

        TempData["DiagOk"] = "Logy byly zcela vyprázdněny (TRUNCATE) a sekvence byla resetována.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Měkčí vyčištění logů – provede DELETE všech záznamů a resetuje sekvenci
    /// pomocí procedury <c>ST72861.PKG_RESET.CLEAR_LOG(FALSE)</c>.
    /// </summary>
    /// <returns>Přesměrování zpět na hlavní stránku administrace s informační zprávou.</returns>
    [ValidateAntiForgeryToken]
    [HttpPost("logs/clear-soft")]
    public async Task<IActionResult> ClearLogsSoft()
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("BEGIN ST72861.PKG_RESET.CLEAR_LOG(FALSE); END;", (OracleConnection)con)
        { CommandType = CommandType.Text };
        await cmd.ExecuteNonQueryAsync();

        TempData["DiagOk"] = "Všechny záznamy logu byly smazány (DELETE) a sekvence byla resetována.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Zkontroluje všechny sekvence a pro prázdné tabulky je resetuje na hodnotu 1
    /// pomocí procedury <c>ST72861.PKG_RESET.RESET_ALL_SEQUENCES</c>.
    /// </summary>
    /// <returns>Přesměrování zpět na hlavní stránku administrace s informační zprávou.</returns>
    [ValidateAntiForgeryToken]
    [HttpPost("sequences/reset")]
    public async Task<IActionResult> ResetSequences()
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand(
            "BEGIN ST72861.PKG_RESET.RESET_ALL_SEQUENCES; END;",
            (OracleConnection)con)
        {
            CommandType = CommandType.Text
        };

        await cmd.ExecuteNonQueryAsync();

        TempData["DiagOk"] = "Sekvence byly zkontrolovány a pro prázdné tabulky resetovány na 1.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Načte stránkovaný seznam logů z pohledu <c>VW_LOGS_ADMIN</c>.
    /// </summary>
    /// <param name="page">Číslo stránky (minimálně 1).</param>
    /// <param name="size">Počet záznamů na stránku (mezi 5 a 200).</param>
    /// <returns>Model <see cref="LogsPageVm"/> s položkami a informací o stránkování.</returns>
    private async Task<LogsPageVm> LoadLogsAsync(int page, int size)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 5, 200);

        await using var con = await _db.CreateOpenAsync();

        long total;
        await using (var countCmd = con.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM VW_LOGS_ADMIN";
            total = Convert.ToInt64(await countCmd.ExecuteScalarAsync());
        }

        var items = new List<LogEntryVm>();
        await using (var cmd = con.CreateCommand())
        {
            cmd.BindByName = true;
            cmd.CommandText = @"
                SELECT LOGID, OPERATIONNAME, TABLENAME, MODIFICATIONDATE, MODIFICATIONBY, OLDVALUES, NEWVALUES
                FROM VW_LOGS_ADMIN
                ORDER BY MODIFICATIONDATE DESC, LOGID DESC
                OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";
            cmd.Parameters.Add("skip", Oracle.ManagedDataAccess.Client.OracleDbType.Int32).Value = (page - 1) * size;
            cmd.Parameters.Add("take", Oracle.ManagedDataAccess.Client.OracleDbType.Int32).Value = size;

            await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
            while (await r.ReadAsync())
            {
                items.Add(new LogEntryVm
                {
                    LogId = r.GetInt64(0),
                    OperationName = r.GetString(1),
                    TableName = r.GetString(2),
                    ModificationDate = r.GetDateTime(3),
                    ModificationBy = r.GetString(4),
                    OldValues = r.IsDBNull(5) ? null : r.GetString(5),
                    NewValues = r.IsDBNull(6) ? null : r.GetString(6)
                });
            }
        }

        return new LogsPageVm
        {
            Items = items,
            PageIndex = page,
            PageSize = size,
            Total = total
        };
    }
}
