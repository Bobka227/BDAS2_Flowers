using System.Data;
using System.Text;
using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.AdminModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Controllers.AdminControllers
{
    /// <summary>
    /// Administrátorský controller pro přehled a export databázových objektů.
    /// Umožňuje zobrazit seznam objektů, načíst jejich zdrojový kód a stáhnout přehled do CSV.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [Route("admin/db-objects")]
    public class AdminDbObjectsController : Controller
    {
        private readonly IDbFactory _db;

        /// <summary>
        /// Inicializuje novou instanci <see cref="AdminDbObjectsController"/> s továrnou databázových připojení.
        /// </summary>
        /// <param name="db">Továrna pro vytváření databázových připojení.</param>
        public AdminDbObjectsController(IDbFactory db)
        {
            _db = db;
        }

        /// <summary>
        /// Načte seznam databázových objektů z pohledu <c>VW_DB_OBJECTS_ADMIN</c>.
        /// </summary>
        /// <returns>Seznam modelů <see cref="DbObjectRowVm"/> reprezentujících databázové objekty.</returns>
        private async Task<List<DbObjectRowVm>> LoadObjectsAsync()
        {
            var list = new List<DbObjectRowVm>();

            const string sql = @"
                SELECT 
                    object_type,
                    object_name,
                    status,
                    created,
                    last_ddl_time
                  FROM ST72861.VW_DB_OBJECTS_ADMIN
                 ORDER BY object_type, object_name";

            await using var conn = await _db.CreateOpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            while (await reader.ReadAsync())
            {
                list.Add(new DbObjectRowVm
                {
                    ObjectType = reader.GetString(0),
                    ObjectName = reader.GetString(1),
                    Status = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Created = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                    LastDdlTime = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4)
                });
            }

            return list;
        }

        /// <summary>
        /// Načte metadata a zdrojový text vybraného databázového objektu.
        /// </summary>
        /// <param name="type">Typ objektu (např. TABLE, VIEW, PACKAGE).</param>
        /// <param name="name">Název objektu v databázi.</param>
        /// <returns>Model <see cref="DbObjectSourceVm"/> se zdrojovým textem nebo <c>null</c>, pokud objekt neexistuje.</returns>
        private async Task<DbObjectSourceVm?> LoadSourceAsync(string type, string name)
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name))
                return null;

            type = type.ToUpperInvariant();
            name = name.ToUpperInvariant();

            await using var conn = await _db.CreateOpenAsync();

            DbObjectSourceVm? vm = null;
            await using (var meta = conn.CreateCommand())
            {
                meta.CommandText = @"
                    SELECT object_type, object_name, status, created, last_ddl_time
                      FROM ST72861.VW_DB_OBJECTS_ADMIN
                     WHERE UPPER(object_type) = :t
                       AND UPPER(object_name) = :n";

                meta.Parameters.Add(new OracleParameter("t", OracleDbType.Varchar2, type, ParameterDirection.Input));
                meta.Parameters.Add(new OracleParameter("n", OracleDbType.Varchar2, name, ParameterDirection.Input));

                await using var r = await meta.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if (!await r.ReadAsync())
                    return null;

                vm = new DbObjectSourceVm
                {
                    ObjectType = r.GetString(0),
                    ObjectName = r.GetString(1),
                    Status = r.IsDBNull(2) ? "" : r.GetString(2),
                    Created = r.IsDBNull(3) ? (DateTime?)null : r.GetDateTime(3),
                    LastDdlTime = r.IsDBNull(4) ? (DateTime?)null : r.GetDateTime(4)
                };
            }

            var sb = new StringBuilder();
            await using (var src = conn.CreateCommand())
            {
                src.CommandText = @"
                    SELECT line, text
                      FROM ST72861.VW_DB_SOURCE_ADMIN
                     WHERE UPPER(object_type) = :t
                       AND UPPER(object_name) = :n
                     ORDER BY line";

                src.Parameters.Add(new OracleParameter("t", OracleDbType.Varchar2, type, ParameterDirection.Input));
                src.Parameters.Add(new OracleParameter("n", OracleDbType.Varchar2, name, ParameterDirection.Input));

                await using var r = await src.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    sb.Append(r.GetString(1));
                }
            }

            vm!.SourceText = sb.ToString();
            return vm;
        }

        /// <summary>
        /// Zobrazí stránku s přehledem databázových objektů.
        /// </summary>
        /// <returns>View s modelem <see cref="DbObjectsVm"/> obsahujícím seznam objektů.</returns>
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var list = await LoadObjectsAsync();
            var vm = new DbObjectsVm { Objects = list };
            return View("~/Views/AdminPanel/DbObjects/Index.cshtml", vm);
        }

        /// <summary>
        /// Vrátí čistý zdrojový text daného databázového objektu.
        /// Používá se např. pro zobrazení v prohlížeči nebo v náhledu.
        /// </summary>
        /// <param name="type">Typ objektu (např. VIEW, PACKAGE, PROCEDURE).</param>
        /// <param name="name">Název objektu v databázi.</param>
        /// <returns>
        /// Obsah zdrojového kódu objektu jako <see cref="ContentResult"/>,
        /// nebo <see cref="NotFoundResult"/>, pokud objekt neexistuje.
        /// </returns>
        [HttpGet("source-text")]
        public async Task<IActionResult> SourceText(string type, string name)
        {
            var vm = await LoadSourceAsync(type, name);
            if (vm is null)
                return NotFound();

            return Content(vm.SourceText, "text/plain; charset=utf-8");
        }

        /// <summary>
        /// Vygeneruje CSV soubor s přehledem databázových objektů a nabídne jej ke stažení.
        /// </summary>
        /// <returns>CSV soubor se seznamem objektů jako <see cref="FileContentResult"/>.</returns>
        [HttpGet("download")]
        public async Task<IActionResult> Download()
        {
            var objects = await LoadObjectsAsync();

            var sb = new StringBuilder();
            sb.AppendLine("ObjectType;ObjectName;Status;Created;LastDdlTime");

            foreach (var o in objects)
            {
                string created = o.Created?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
                string ddl = o.LastDdlTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";

                string name = o.ObjectName.Replace("\"", "\"\"");
                string type = o.ObjectType.Replace("\"", "\"\"");
                string stat = (o.Status ?? "").Replace("\"", "\"\"");

                sb.AppendLine($"{type};\"{name}\";\"{stat}\";{created};{ddl}");
            }

            var csv = "\uFEFF" + sb.ToString();
            var bytes = Encoding.UTF8.GetBytes(csv);
            var fileName = $"db_objects_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

            return File(bytes, "text/csv; charset=utf-8", fileName);
        }
    }
}
