using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.EventModels;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers.EventControllers
{
    public class EventsController : Controller
    {
        private readonly IDbFactory _db;
        private readonly IWebHostEnvironment _env;

        public EventsController(IDbFactory db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        [HttpGet("/events/{id:int}")]
        public async Task<IActionResult> Type(int id)
        {
            EventTypeVm? vm = null;

            await using (var conn = await _db.CreateOpenAsync())
            await using (var cmd = conn.CreateCommand())
            {
                // TODO VIEW
                cmd.CommandText = @"
                    SELECT eventtypeid, eventname, description
                    FROM event_type
                    WHERE eventtypeid = :id";
                cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Int32, id, ParameterDirection.Input));

                await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow | CommandBehavior.CloseConnection);
                if (await r.ReadAsync())
                {
                    var iId = r.GetOrdinal("eventtypeid");
                    var iName = r.GetOrdinal("eventname");
                    var iDesc = r.GetOrdinal("description");

                    vm = new EventTypeVm
                    {
                        EventTypeId = r.GetInt32(iId),
                        Name = r.GetString(iName),
                        Description = r.IsDBNull(iDesc) ? "" : r.GetString(iDesc)
                    };
                }
            }

            if (vm is null) return NotFound();

            var dir = Path.Combine(_env.WebRootPath, "images", "events");
            var urls = new List<string>();
            if (Directory.Exists(dir))
            {
                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

                urls = Directory.EnumerateFiles(dir)
                                .Where(p => allowed.Contains(Path.GetExtension(p)))
                                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                                .Select(p => $"/images/events/{Path.GetFileName(p)}")
                                .ToList();
            }
            ViewBag.Images = urls;
            ViewBag.HeroBg = urls.FirstOrDefault() ?? "/images/events/01.jpg";

            return View("Type", vm);
        }
    }
}
