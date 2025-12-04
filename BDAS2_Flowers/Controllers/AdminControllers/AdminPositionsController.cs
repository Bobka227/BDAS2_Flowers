using System.Data;
using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.AdminModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Controllers.AdminControllers;

[Authorize(Roles = "Admin")]
[Route("admin/positions")]
public class AdminPositionsController : Controller
{
    private readonly IDbFactory _db;
    public AdminPositionsController(IDbFactory db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var rows = new List<AdminPositionRowVm>();

        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT ID,
               NAME,
               EMP_COUNT
          FROM VW_ADMIN_POSITIONS
         ORDER BY NAME";

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(new AdminPositionRowVm
            {
                Id = DbRead.GetInt32(r, 0),
                Name = r.GetString(1),
                EmployeesCount = DbRead.GetInt32(r, 2)
            });
        }

        return View("/Views/AdminPanel/Positions/Index.cshtml", rows);
    }


    [HttpGet("create")]
    public IActionResult Create()
    {
        var vm = new AdminPositionEditVm
        {
            EmployeeCount = 0
        };
        return View("/Views/AdminPanel/Positions/Edit.cshtml", vm);
    }


    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminPositionEditVm vm)
    {
        if (!ModelState.IsValid)
            return View("/Views/AdminPanel/Positions/Edit.cshtml", vm);

        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_POSITION_CREATE", (OracleConnection)conn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };
        cmd.Parameters.Add("p_name", OracleDbType.Varchar2).Value = vm.Name.Trim();

        await cmd.ExecuteNonQueryAsync();
        TempData["Msg"] = "Pozice byla vytvořena.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = new AdminPositionEditVm();

        await using var conn = await _db.CreateOpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
          SELECT ID,
                 NAME,
                 EMP_COUNT
            FROM VW_ADMIN_POSITIONS
           WHERE ID = :id";

            cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Int32, id, ParameterDirection.Input));

            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync())
                return NotFound();

            vm.Id = DbRead.GetInt32(r, 0);
            vm.Name = r.GetString(1);
            vm.EmployeeCount = DbRead.GetInt32(r, 2);
        }

        return View("/Views/AdminPanel/Positions/Edit.cshtml", vm);
    }

    [HttpPost("edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminPositionEditVm vm)
    {
        if (id != vm.Id)
            return BadRequest();

        if (!ModelState.IsValid)
            return View("/Views/AdminPanel/Positions/Edit.cshtml", vm);

        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_POSITION_UPDATE", (OracleConnection)conn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };

        cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = vm.Id;
        cmd.Parameters.Add("p_name", OracleDbType.Varchar2).Value = vm.Name.Trim();

        await cmd.ExecuteNonQueryAsync();
        TempData["Msg"] = "Pozice byla upravena.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_POSITION_DELETE", (OracleConnection)conn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };
        cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = id;

        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = "Pozice byla smazána.";
        }
        catch (OracleException ex)
        {
            TempData["Msg"] = "Nelze smazat pozici: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
