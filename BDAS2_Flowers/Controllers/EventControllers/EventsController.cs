using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.EventModels;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers.EventControllers
{
    /// <summary>
    /// Public controller pro zobrazení detailu typu události (např. svatba, oslava).
    /// Z databáze načítá informace o typu události a k tomu připravuje sadu ukázkových obrázků.
    /// </summary>
    public class EventsController : Controller
    {
        private readonly IDbFactory _db;
        private readonly IWebHostEnvironment _env;

        /// <summary>
        /// Inicializuje novou instanci <see cref="EventsController"/> s přístupem k databázi
        /// a informacím o webovém prostředí (např. cesta k wwwroot).
        /// </summary>
        /// <param name="db">Továrna pro vytváření a otevírání databázových připojení.</param>
        /// <param name="env">Prostředí aplikace, sloužící zejména k získání fyzických cest k souborům.</param>
        public EventsController(IDbFactory db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        /// <summary>
        /// Zobrazí stránku s popisem konkrétního typu události.
        /// Kromě údajů z databáze také vyhledá ilustrační obrázky v adresáři
        /// <c>wwwroot/images/events</c> a předá je do view.
        /// </summary>
        /// <param name="id">Identifikátor typu události.</param>
        /// <returns>
        /// View <c>Type</c> s modelem <see cref="EventTypeVm"/>,
        /// nebo <see cref="NotFoundResult"/>, pokud typ události neexistuje.
        /// </returns>
        [HttpGet("/events/{id:int}")]
        public async Task<IActionResult> Type(int id)
        {
            EventTypeVm? vm = null;

            await using (var conn = await _db.CreateOpenAsync())
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT ID,
                           NAME,
                           DESCRIPTION
                      FROM VW_EVENT_TYPES
                     WHERE ID = :id";

                cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Int32, id, ParameterDirection.Input));

                await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow | CommandBehavior.CloseConnection);
                if (await r.ReadAsync())
                {
                    vm = new EventTypeVm
                    {
                        EventTypeId = r.GetInt32(0),
                        Name = r.GetString(1),
                        Description = r.IsDBNull(2) ? "" : r.GetString(2)
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
