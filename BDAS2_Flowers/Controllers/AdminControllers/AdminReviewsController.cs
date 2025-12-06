using System.Collections.Generic;
using System.Threading.Tasks;
using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.AdminModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers.AdminControllers
{
    /// <summary>
    /// Administrátorský controller pro správu uživatelských recenzí.
    /// Umožňuje recenze zobrazit a mazat pomocí uložené procedury v databázi.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [Route("admin/reviews")]
    public class AdminReviewsController : Controller
    {
        private readonly IDbFactory _db;

        /// <summary>
        /// Inicializuje novou instanci <see cref="AdminReviewsController"/> s továrnou databázových připojení.
        /// </summary>
        /// <param name="db">Továrna pro vytváření a otevírání databázových připojení.</param>
        public AdminReviewsController(IDbFactory db) => _db = db;

        /// <summary>
        /// Zobrazí seznam všech recenzí z pohledu <c>VW_REVIEWS</c>.
        /// </summary>
        /// <returns>
        /// View s kolekcí <see cref="AdminReviewRowVm"/> pro administrátorský přehled recenzí.
        /// </returns>
        // GET /admin/reviews
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var rows = new List<AdminReviewRowVm>();

            await using var con = (OracleConnection)await _db.CreateOpenAsync();
            await using var cmd = con.CreateCommand();
            cmd.BindByName = true;
            cmd.CommandText = @"
                SELECT REVIEWID,
                       DISPLAY_NAME,
                       CITY,
                       STARS,
                       CREATED,
                       REVIEW_TEXT
                  FROM ST72861.VW_REVIEWS
                 ORDER BY CREATED DESC";

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                rows.Add(new AdminReviewRowVm
                {
                    ReviewId = r.GetInt32(0),
                    UserDisplay = r.GetString(1),
                    City = r.IsDBNull(2) ? "" : r.GetString(2),
                    Stars = r.GetInt32(3),
                    Created = r.GetDateTime(4),
                    Text = r.GetString(5)
                });
            }

            return View("/Views/AdminPanel/Reviews/Index.cshtml", rows);
        }

        /// <summary>
        /// Smaže recenzi podle jejího ID pomocí uložené procedury <c>ST72861.PRC_REVIEW_DELETE</c>.
        /// </summary>
        /// <param name="id">Identifikátor mazané recenze.</param>
        /// <returns>
        /// Přesměrování zpět na seznam recenzí s informační zprávou
        /// o úspěchu nebo chybě operace.
        /// </returns>
        // POST /admin/reviews/delete/123
        [HttpPost("delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await using var con = (OracleConnection)await _db.CreateOpenAsync();
            await using var cmd = new OracleCommand("ST72861.PRC_REVIEW_DELETE", con)
            {
                CommandType = CommandType.StoredProcedure,
                BindByName = true
            };

            cmd.Parameters.Add("p_review_id", OracleDbType.Int32).Value = id;
            cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100)
                          .Value = User.Identity?.Name ?? "admin";

            try
            {
                await cmd.ExecuteNonQueryAsync();
                TempData["DiagOk"] = "Recenze byla smazána.";
            }
            catch (OracleException ex)
            {
                TempData["DiagErr"] = "Chyba při mazání recenze: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
