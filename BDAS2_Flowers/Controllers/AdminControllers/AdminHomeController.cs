using BDAS2_Flowers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using BDAS2_Flowers.Models.ViewModels.AdminModels;

namespace BDAS2_Flowers.Controllers.AdminControllers;

[Authorize(Roles = "Admin")]
[Route("admin")]
public class AdminHomeController : Controller
{
    private readonly IDbFactory _db;
    public AdminHomeController(IDbFactory db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, int size = 20)
    {
        ViewData["Title"] = "Admin";
        var logs = await LoadLogsAsync(page, size);
        return View("/Views/AdminPanel/Index.cshtml", logs);
    }


    [HttpGet("logs")]
    public async Task<IActionResult> Logs(int page = 1, int size = 20)
    {
        var logs = await LoadLogsAsync(page, size);
        return PartialView("/Views/AdminPanel/_LogsTable.cshtml", logs);
    }

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
