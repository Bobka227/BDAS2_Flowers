using System.Data;
using System.Text;                        
using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.AdminModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Controllers.AdminControllers
{
    [Authorize(Roles = "Admin")]
    [Route("admin/db-objects")]
    public class AdminDbObjectsController : Controller
    {
        private readonly IDbFactory _db;

        public AdminDbObjectsController(IDbFactory db)
        {
            _db = db;
        }

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
                                FROM   user_objects
                                WHERE  object_type IN (
                                           'TABLE',
                                           'VIEW',
                                           'SEQUENCE',
                                           'PROCEDURE',
                                           'FUNCTION',
                                           'PACKAGE',
                                           'PACKAGE BODY',
                                           'TRIGGER'
                                       )
                                ORDER BY 
                                    object_type,
                                    object_name";

            await using var conn = await _db.CreateOpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            while (await reader.ReadAsync())
            {
                var row = new DbObjectRowVm
                {
                    ObjectType = reader.GetString(0),
                    ObjectName = reader.GetString(1),
                    Status = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Created = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                    LastDdlTime = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4)
                };

                list.Add(row);
            }

            return list;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var list = await LoadObjectsAsync();
            var vm = new DbObjectsVm { Objects = list };
            return View("~/Views/AdminPanel/DbObjects/Index.cshtml", vm);
        }

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

                sb.AppendLine(
                    $"{type};\"{name}\";\"{stat}\";{created};{ddl}"
                );
            }

            var csv = "\uFEFF" + sb.ToString();
            var bytes = Encoding.UTF8.GetBytes(csv);
            var fileName = $"db_objects_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

            return File(bytes, "text/csv; charset=utf-8", fileName);
        }
    }
}
