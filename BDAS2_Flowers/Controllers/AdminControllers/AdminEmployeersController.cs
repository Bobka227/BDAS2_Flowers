using System.Data;
using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.AdminModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Controllers.AdminControllers;

/// <summary>
/// Administrátorský controller pro správu zaměstnanců.
/// Umožňuje zobrazit seznam, vytvářet, editovat, mazat zaměstnance a pracovat se stromovou strukturou řízení.
/// </summary>
[Authorize(Roles = "Admin")]
[Route("admin/employeers")]
public class AdminEmployeersController : Controller
{
    private readonly IDbFactory _db;

    /// <summary>
    /// Inicializuje novou instanci <see cref="AdminEmployeersController"/> s továrnou databázových připojení.
    /// </summary>
    /// <param name="db">Továrna pro vytváření a otevírání databázových připojení.</param>
    public AdminEmployeersController(IDbFactory db) => _db = db;

    /// <summary>
    /// Zobrazí seznam všech zaměstnanců včetně informací o jejich pozici, obchodu, nadřízeném a souhrnném platu týmu.
    /// </summary>
    /// <returns>View s kolekcí <see cref="AdminEmployeeRowVm"/>.</returns>
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var rows = new List<AdminEmployeeRowVm>();

        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT ID, FIRSTNAME, LASTNAME, EMPLOYMENTDATE, SALARY,
                 SHOP, POSITION, MANAGER, TEAM_SALARY
            FROM ST72861.VW_ADMIN_EMPLOYEERS
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
                Manager = r.IsDBNull(7) ? "" : r.GetString(7),
                TeamSalary = r.IsDBNull(8) ? 0 : (decimal)r.GetDecimal(8)
            });
        }

        return View("/Views/AdminPanel/Employeers/Index.cshtml", rows);
    }

    /// <summary>
    /// Zobrazí formulář pro vytvoření nového zaměstnance.
    /// </summary>
    /// <returns>View s editačním modelem <see cref="AdminEmployeeEditVm"/> a naplněnými číselníky.</returns>
    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        var vm = new AdminEmployeeEditVm { EmploymentDate = DateTime.Today };
        await FillLookupsAsync(vm);
        return View("/Views/AdminPanel/Employeers/Edit.cshtml", vm);
    }

    /// <summary>
    /// Vytvoří nového zaměstnance pomocí uložené procedury <c>PRC_EMPLOYEER_CREATE</c>.
    /// </summary>
    /// <param name="vm">Model obsahující údaje o zaměstnanci k vytvoření.</param>
    /// <returns>
    /// Při úspěchu přesměruje na seznam zaměstnanců,
    /// při chybné validaci opět zobrazí formulář s chybami.
    /// </returns>
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

    /// <summary>
    /// Zobrazí formulář pro úpravu existujícího zaměstnance.
    /// </summary>
    /// <param name="id">Identifikátor upravovaného zaměstnance.</param>
    /// <returns>
    /// View s vyplněným modelem <see cref="AdminEmployeeEditVm"/>,
    /// nebo <see cref="NotFoundResult"/>, pokud zaměstnanec neexistuje.
    /// </returns>
    [HttpGet("edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = new AdminEmployeeEditVm();

        await using var conn = await _db.CreateOpenAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
          SELECT ID,
                 FIRSTNAME,
                 LASTNAME,
                 EMPLOYMENTDATE,
                 SALARY,
                 SHOPID,
                 MANAGERID,
                 POSITIONID
            FROM VW_ADMIN_EMPLOYEERS
           WHERE ID = :id";

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

    /// <summary>
    /// Uloží změny existujícího zaměstnance pomocí uložené procedury <c>PRC_EMPLOYEER_UPDATE</c>.
    /// </summary>
    /// <param name="id">Identifikátor zaměstnance z URL.</param>
    /// <param name="vm">Model s upravenými údaji zaměstnance.</param>
    /// <returns>
    /// Při úspěchu přesměruje na seznam zaměstnanců,
    /// při chybné validaci opět zobrazí formulář s chybami.
    /// </returns>
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

    /// <summary>
    /// Smaže zaměstnance pomocí uložené procedury <c>PRC_EMPLOYEER_DELETE</c>.
    /// </summary>
    /// <param name="id">Identifikátor mazáného zaměstnance.</param>
    /// <returns>Přesměrování zpět na seznam zaměstnanců s výslednou zprávou.</returns>
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

    /// <summary>
    /// Zobrazí strom organizační struktury zaměstnanců (nadřízený–podřízený).
    /// </summary>
    /// <returns>
    /// View s kolekcí <see cref="AdminEmployeeTreeRowVm"/> a pomocným seznamem zaměstnanců v <c>ViewBag.Employees</c>.
    /// </returns>
    [HttpGet("tree")]
    public async Task<IActionResult> Tree()
    {
        var rows = new List<AdminEmployeeTreeRowVm>();
        var employees = new List<(int Id, string Name)>();

        await using var conn = await _db.CreateOpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
          SELECT t.ID,
                 t.INDENTED_NAME,
                 t.LVL,
                 t.ROOT_NAME,
                 t.ORG_PATH,
                 t.SHOP_NAME,
                 t.POSITION_NAME,
                 m.FIRSTNAME || ' ' || m.LASTNAME AS MANAGER_NAME
            FROM VW_EMPLOYEER_TREE t
            LEFT JOIN EMPLOYEER m ON m.EMPLOYEERID = t.MANAGERID
           ORDER BY t.ROOT_ID, t.LVL, t.LASTNAME, t.FIRSTNAME";

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                rows.Add(new AdminEmployeeTreeRowVm
                {
                    Id = DbRead.GetInt32(r, 0),
                    IndentedName = r.GetString(1),
                    Level = DbRead.GetInt32(r, 2),
                    RootName = r.IsDBNull(3) ? "" : r.GetString(3),
                    OrgPath = r.IsDBNull(4) ? "" : r.GetString(4),
                    Shop = r.IsDBNull(5) ? "" : r.GetString(5),
                    Position = r.IsDBNull(6) ? "" : r.GetString(6),
                    Manager = r.IsDBNull(7) ? "" : r.GetString(7)
                });
            }
        }

        await using (var cmd2 = conn.CreateCommand())
        {
            cmd2.CommandText = @"
               SELECT ID,
               FIRSTNAME || ' ' || LASTNAME AS FULLNAME
               FROM VW_EMPLOYEER_TREE
               ORDER BY LASTNAME, FIRSTNAME";

            await using var r2 = await cmd2.ExecuteReaderAsync();
            while (await r2.ReadAsync())
            {
                employees.Add((DbRead.GetInt32(r2, 0), r2.GetString(1)));
            }
        }

        ViewBag.Employees = employees;

        return View("/Views/AdminPanel/Employeers/Tree.cshtml", rows);
    }

    /// <summary>
    /// Přesune celý podstrom zaměstnance pod nového nadřízeného
    /// pomocí uložené procedury <c>ST72861.PRC_EMP_MOVE_SUBTREE</c>.
    /// </summary>
    /// <param name="empId">Identifikátor zaměstnance, jehož podstrom se přesouvá.</param>
    /// <param name="newManagerId">Identifikátor nového nadřízeného.</param>
    /// <returns>
    /// Přesměrování zpět na stránku se stromem s informační zprávou o výsledku operace.
    /// </returns>
    [HttpPost("move-subtree")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveSubtree(int empId, int newManagerId)
    {
        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("ST72861.PRC_EMP_MOVE_SUBTREE", (OracleConnection)conn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };

        cmd.Parameters.Add("p_emp_id", OracleDbType.Int32).Value = empId;
        cmd.Parameters.Add("p_new_manager_id", OracleDbType.Int32).Value = newManagerId;
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2).Value =
            User.Identity?.Name ?? "admin";

        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = "Podstrom zaměstnance byl úspěšně přesunut.";
        }
        catch (OracleException ex)
        {
            TempData["Msg"] = "Chyba při přesunu podstromu: " + ex.Message;
        }

        return RedirectToAction(nameof(Tree));
    }

    /// <summary>
    /// Naplní view model <see cref="AdminEmployeeEditVm"/> potřebnými číselníky
    /// (obchody, pozice, potenciální nadřízení) a uloží je do <c>ViewBag</c>.
    /// </summary>
    /// <param name="vm">Model zaměstnance, pro kterého se lookup hodnoty načítají.</param>
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
