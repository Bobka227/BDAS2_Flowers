using System.Data;
using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.AdminModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Controllers.AdminControllers;

[Authorize(Roles = "Admin")]
[Route("admin/employeers")]
public class AdminEmployeersController : Controller
{
    private readonly IDbFactory _db;
    public AdminEmployeersController(IDbFactory db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var rows = new List<AdminEmployeeRowVm>();

        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
          SELECT ID, FIRSTNAME, LASTNAME, EMPLOYMENTDATE, SALARY,
                 SHOP, POSITION, MANAGER
            FROM VW_ADMIN_EMPLOYEERS
           ORDER BY LASTNAME, FIRSTNAME";

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(new AdminEmployeeRowVm
            {
                Id = DbRead.GetInt32(r, 0),
                FirstName = r.GetString(1),
                LastName = r.GetString(2),
                EmploymentDate = r.GetDateTime(3),
                Salary = (decimal)r.GetDecimal(4),
                Shop = r.IsDBNull(5) ? "" : r.GetString(5),
                Position = r.IsDBNull(6) ? "" : r.GetString(6),
                Manager = r.IsDBNull(7) ? "" : r.GetString(7)
            });
        }

        return View("/Views/AdminPanel/Employeers/Index.cshtml", rows);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        var vm = new AdminEmployeeEditVm { EmploymentDate = DateTime.Today };
        await FillLookupsAsync(vm);
        return View("/Views/AdminPanel/Employeers/Edit.cshtml", vm);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminEmployeeEditVm vm)
    {
        if (!ModelState.IsValid)
        {
            await FillLookupsAsync(vm);
            return View("/Views/AdminPanel/Employeers/Edit.cshtml", vm);
        }

        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_EMPLOYEER_CREATE", (OracleConnection)conn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };

        cmd.Parameters.Add("p_employmentdate", OracleDbType.Date).Value = vm.EmploymentDate;
        cmd.Parameters.Add("p_salary", OracleDbType.Decimal).Value = vm.Salary;
        cmd.Parameters.Add("p_firstname", OracleDbType.Varchar2).Value = vm.FirstName.Trim();
        cmd.Parameters.Add("p_lastname", OracleDbType.Varchar2).Value = vm.LastName.Trim();
        cmd.Parameters.Add("p_shopid", OracleDbType.Int32).Value = vm.ShopId;
        cmd.Parameters.Add("p_managerid", OracleDbType.Int32).Value =
            (object?)vm.ManagerId ?? DBNull.Value;
        cmd.Parameters.Add("p_positionid", OracleDbType.Int32).Value =
            (object?)vm.PositionId ?? DBNull.Value;

        await cmd.ExecuteNonQueryAsync();

        TempData["Msg"] = "Zaměstnanec byl vytvořen.";
        return RedirectToAction(nameof(Index));
    }


    [HttpGet("edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = new AdminEmployeeEditVm();

        await using var conn = await _db.CreateOpenAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
              SELECT EMPLOYEERID, FIRSTNAME, LASTNAME,
                     EMPLOYMENTDATE, SALARY,
                     SHOPID, MANAGERID, POSITIONID
                FROM EMPLOYEER
               WHERE EMPLOYEERID = :id";
            cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Int32, id, ParameterDirection.Input));

            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync())
                return NotFound();

            vm.Id = DbRead.GetInt32(r, 0);
            vm.FirstName = r.GetString(1);
            vm.LastName = r.GetString(2);
            vm.EmploymentDate = r.GetDateTime(3);
            vm.Salary = (decimal)r.GetDecimal(4);
            vm.ShopId = DbRead.GetInt32(r, 5);
            vm.ManagerId = r.IsDBNull(6) ? (int?)null : DbRead.GetInt32(r, 6);
            vm.PositionId = r.IsDBNull(7) ? (int?)null : DbRead.GetInt32(r, 7);
        }

        await FillLookupsAsync(vm);
        return View("/Views/AdminPanel/Employeers/Edit.cshtml", vm);
    }

    [HttpPost("edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminEmployeeEditVm vm)
    {
        if (id != vm.Id)
            return BadRequest();

        if (!ModelState.IsValid)
        {
            await FillLookupsAsync(vm);
            return View("/Views/AdminPanel/Employeers/Edit.cshtml", vm);
        }

        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_EMPLOYEER_UPDATE", (OracleConnection)conn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };

        cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = vm.Id;
        cmd.Parameters.Add("p_employmentdate", OracleDbType.Date).Value = vm.EmploymentDate;
        cmd.Parameters.Add("p_salary", OracleDbType.Decimal).Value = vm.Salary;
        cmd.Parameters.Add("p_firstname", OracleDbType.Varchar2).Value = vm.FirstName.Trim();
        cmd.Parameters.Add("p_lastname", OracleDbType.Varchar2).Value = vm.LastName.Trim();
        cmd.Parameters.Add("p_shopid", OracleDbType.Int32).Value = vm.ShopId;
        cmd.Parameters.Add("p_managerid", OracleDbType.Int32).Value =
            (object?)vm.ManagerId ?? DBNull.Value;
        cmd.Parameters.Add("p_positionid", OracleDbType.Int32).Value =
            (object?)vm.PositionId ?? DBNull.Value;

        await cmd.ExecuteNonQueryAsync();

        TempData["Msg"] = "Zaměstnanec byl upraven.";
        return RedirectToAction(nameof(Index));
    }


    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_EMPLOYEER_DELETE", (OracleConnection)conn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };
        cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = id;

        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = "Zaměstnanec byl smazán.";
        }
        catch (OracleException ex)
        {
            TempData["Msg"] = "Nelze smazat zaměstnance: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }


    [HttpGet("tree")]
    public async Task<IActionResult> Tree()
    {
        var rows = new List<(int Id, string Name, int Level, string Manager)>();

        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
      SELECT t.ID,
             t.INDENTED_NAME,
             t.LVL,
             m.FIRSTNAME || ' ' || m.LASTNAME AS MANAGER_NAME
        FROM VW_EMPLOYEER_TREE t
        LEFT JOIN EMPLOYEER m ON m.EMPLOYEERID = t.MANAGERID
       ORDER BY t.LVL, t.LASTNAME, t.FIRSTNAME";

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add((
                DbRead.GetInt32(r, 0),
                r.GetString(1),
                DbRead.GetInt32(r, 2),
                r.IsDBNull(3) ? "" : r.GetString(3)
            ));
        }

        return View("/Views/AdminPanel/Employeers/Tree.cshtml", rows);
    }


    private async Task FillLookupsAsync(AdminEmployeeEditVm vm)
    {
        await using var conn = await _db.CreateOpenAsync();

        var shops = new List<(int Id, string Name)>();
        await using (var c = conn.CreateCommand())
        {
            c.CommandText = "SELECT SHOPID, NAME FROM FLOWER_SHOP ORDER BY NAME";
            await using var r = await c.ExecuteReaderAsync();
            while (await r.ReadAsync())
                shops.Add((DbRead.GetInt32(r, 0), r.GetString(1)));
        }

        var positions = new List<(int Id, string Name)>();
        await using (var c = conn.CreateCommand())
        {
            c.CommandText = "SELECT POSITIONID, POSITIONNAME FROM POSITION ORDER BY POSITIONNAME";
            await using var r = await c.ExecuteReaderAsync();
            while (await r.ReadAsync())
                positions.Add((DbRead.GetInt32(r, 0), r.GetString(1)));
        }

        var managers = new List<(int Id, string Name)>();
        await using (var c = conn.CreateCommand())
        {
            c.CommandText = @"
              SELECT EMPLOYEERID, FIRSTNAME || ' ' || LASTNAME
                FROM EMPLOYEER
               ORDER BY LASTNAME, FIRSTNAME";
            await using var r = await c.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var id = DbRead.GetInt32(r, 0);
                if (vm.Id.HasValue && vm.Id.Value == id) 
                    continue;
                managers.Add((id, r.GetString(1)));
            }
        }

        ViewBag.Shops = shops;
        ViewBag.Positions = positions;
        ViewBag.Managers = managers;
    }
}
