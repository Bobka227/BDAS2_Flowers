using System.Collections.Generic;
using System.Threading.Tasks;
using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.AdminModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Controllers.AdminControllers
{
    /// <summary>
    /// Administrátorský controller pro práci s událostmi souvisejícími s objednávkami.
    /// Umožňuje zobrazit seznam událostí a jejich přehled v kalendáři.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [Route("admin/events")]
    public class AdminEventsController : Controller
    {
        private readonly IDbFactory _db;

        /// <summary>
        /// Inicializuje novou instanci <see cref="AdminEventsController"/> s továrnou databázových připojení.
        /// </summary>
        /// <param name="db">Továrna pro vytváření a otevírání databázových připojení.</param>
        public AdminEventsController(IDbFactory db) => _db = db;

        /// <summary>
        /// Zobrazí seznam všech událostí v systému včetně souvisejících údajů o objednávce.
        /// </summary>
        /// <returns>View s kolekcí <see cref="AdminEventRowVm"/> reprezentující jednotlivé události.</returns>
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var rows = new List<AdminEventRowVm>();

            await using var con = await _db.CreateOpenAsync();
            await using var cmd = con.CreateCommand();
            cmd.BindByName = true;

            cmd.CommandText = @"
                SELECT EVENTID,
                       ORDER_NO,
                       EVENTDATE,
                       EVENT_TYPE,
                       CUSTOMER,
                       STATUS,
                       DELIVERY,
                       SHOP,
                       TOTAL
                  FROM ST72861.VW_ADMIN_EVENTS
                 ORDER BY EVENTDATE DESC";

            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                rows.Add(new AdminEventRowVm
                {
                    EventId = rd.GetInt32(0),
                    OrderNo = rd.GetString(1),
                    EventDate = rd.GetDateTime(2),
                    EventType = rd.GetString(3),
                    Customer = rd.GetString(4),
                    Status = rd.GetString(5),
                    Delivery = rd.GetString(6),
                    Shop = rd.GetString(7),
                    Total = rd.GetDecimal(8)
                });
            }

            ViewData["Title"] = "Události";
            return View("/Views/AdminPanel/Events/Index.cshtml", rows);
        }

        /// <summary>
        /// Zobrazí události pro daný měsíc v podobě kalendáře.
        /// </summary>
        /// <param name="year">Rok, pro který se má kalendář zobrazit. Pokud není zadán, použije se aktuální rok.</param>
        /// <param name="month">Měsíc (1–12), pro který se má kalendář zobrazit. Pokud není zadán, použije se aktuální měsíc.</param>
        /// <returns>
        /// View s modelem <see cref="AdminCalendarVm"/>, který obsahuje události v daném měsíci
        /// a metadata pro vykreslení kalendáře.
        /// </returns>
        [HttpGet("calendar")]
        public async Task<IActionResult> Calendar(int? year, int? month)
        {
            var today = DateTime.Today;
            var y = year ?? today.Year;
            var m = month ?? today.Month;

            var firstDay = new DateTime(y, m, 1);
            var nextMonth = firstDay.AddMonths(1);

            var rows = new List<AdminEventRowVm>();

            await using (var conn = await _db.CreateOpenAsync())
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT
                        EVENTID,
                        ORDER_NO,
                        EVENTDATE,
                        EVENT_TYPE,
                        CUSTOMER,
                        STATUS,
                        DELIVERY,
                        SHOP,
                        TOTAL
                    FROM VW_ADMIN_EVENTS
                    WHERE EVENTDATE >= :fromDate
                      AND EVENTDATE <  :toDate
                    ORDER BY EVENTDATE";

                cmd.Parameters.Add(new OracleParameter("fromDate", firstDay));
                cmd.Parameters.Add(new OracleParameter("toDate", nextMonth));

                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    rows.Add(new AdminEventRowVm
                    {
                        EventId = rd.GetInt32(0),
                        OrderNo = rd.GetString(1),
                        EventDate = rd.GetDateTime(2),
                        EventType = rd.GetString(3),
                        Customer = rd.GetString(4),
                        Status = rd.GetString(5),
                        Delivery = rd.GetString(6),
                        Shop = rd.GetString(7),
                        Total = rd.GetDecimal(8)
                    });
                }
            }

            var model = new AdminCalendarVm
            {
                Year = y,
                Month = m,
                Events = rows
            };

            ViewData["Title"] = "Kalendář událostí";
            return View("/Views/AdminPanel/Events/Calendar.cshtml", model);
        }
    }
}
