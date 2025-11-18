using System.Collections.Generic;
using System.Threading.Tasks;
using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.AdminModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Controllers.AdminControllers
{
    [Authorize(Roles = "Admin")]
    [Route("admin/events")]
    public class AdminEventsController : Controller
    {
        private readonly IDbFactory _db;
        public AdminEventsController(IDbFactory db) => _db = db;

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
    }
}
