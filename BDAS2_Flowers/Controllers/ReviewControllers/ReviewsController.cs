using System.Data;
using System.Security.Claims;
using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.ReviewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Controllers.ReviewControllers
{
    /// <summary>
    /// Řadič pro práci se zákaznickými recenzemi.
    /// Zajišťuje zobrazení všech recenzí a přidávání nových hodnocení
    /// přihlášenými uživateli.
    /// </summary>
    public class ReviewsController : Controller
    {
        private readonly IDbFactory _db;

        /// <summary>
        /// Vytvoří novou instanci řadiče recenzí s přístupem k databázi.
        /// </summary>
        /// <param name="db">Továrna na databázová připojení.</param>
        public ReviewsController(IDbFactory db) => _db = db;

        /// <summary>
        /// Vrátí interní ID aktuálně přihlášeného uživatele
        /// z <see cref="ClaimTypes.NameIdentifier"/>.
        /// </summary>
        private int CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        /// <summary>
        /// Zobrazí stránku s přehledem recenzí.
        /// Načte data z pohledu <c>VW_REVIEWS</c> a předá je do view modelu.
        /// </summary>
        /// <returns>
        /// View s modelem <see cref="ReviewsPageVm"/> obsahujícím seznam recenzí.
        /// </returns>
        [HttpGet("/Recenze")]
        public async Task<IActionResult> Index()
        {
            var vm = new ReviewsPageVm();

            await using var con = (OracleConnection)await _db.CreateOpenAsync();
            await using var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT DISPLAY_NAME,
                       CITY,
                       STARS,
                       CREATED,
                       REVIEW_TEXT
                  FROM ST72861.VW_REVIEWS
                 ORDER BY CREATED DESC";

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                vm.Reviews.Add(new ReviewRowVm
                {
                    Name = r.IsDBNull(0) ? "Anonym" : r.GetString(0),
                    City = r.IsDBNull(1) ? "" : r.GetString(1),
                    Stars = r.GetInt32(2),
                    Created = r.GetDateTime(3),
                    Text = r.GetString(4)
                });
            }

            return View("~/Views/Shared/Components/InfoPages/Recenze.cshtml", vm);
        }

        /// <summary>
        /// Přidá novou recenzi od přihlášeného uživatele.
        /// Ověří vstupní hodnoty, v případě chyby znovu zobrazí stránku s recenzemi,
        /// jinak zavolá proceduru <c>PRC_REVIEW_CREATE</c> a recenzi uloží.
        /// </summary>
        /// <param name="m">
        /// View model se vstupními daty nové recenze
        /// (počet hvězdiček, text, volitelně město).
        /// </param>
        /// <returns>
        /// Při chybné validaci vrací view se zobrazenými chybami,
        /// jinak redirect na akci <see cref="Index"/>.
        /// </returns>
        [Authorize]
        [HttpPost("/Recenze")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(ReviewsPageVm m)
        {
            if (m.NewStars is null || m.NewStars < 1 || m.NewStars > 5)
                ModelState.AddModelError(nameof(m.NewStars), "Vyberte 1 až 5 hvězdiček.");
            if (string.IsNullOrWhiteSpace(m.NewText))
                ModelState.AddModelError(nameof(m.NewText), "Napište krátkou recenzi.");

            var userId = CurrentUserId;
            if (userId <= 0) return Forbid();

            await using var con = (OracleConnection)await _db.CreateOpenAsync();

            if (!ModelState.IsValid)
            {
                var vm = new ReviewsPageVm
                {
                    NewStars = m.NewStars,
                    NewText = m.NewText,
                    NewCity = m.NewCity
                };

                await using var cmdLoad = con.CreateCommand();
                cmdLoad.CommandText = @"
                    SELECT DISPLAY_NAME,
                           CITY,
                           STARS,
                           CREATED,
                           REVIEW_TEXT
                      FROM ST72861.VW_REVIEWS
                     ORDER BY CREATED DESC";

                await using var r = await cmdLoad.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    vm.Reviews.Add(new ReviewRowVm
                    {
                        Name = r.IsDBNull(0) ? "Anonym" : r.GetString(0),
                        City = r.IsDBNull(1) ? "" : r.GetString(1),
                        Stars = r.GetInt32(2),
                        Created = r.GetDateTime(3),
                        Text = r.GetString(4)
                    });
                }

                return View("~/Views/Shared/Components/InfoPages/Recenze.cshtml", vm);
            }

            await using var cmd = new OracleCommand("ST72861.PRC_REVIEW_CREATE", con)
            {
                CommandType = CommandType.StoredProcedure,
                BindByName = true
            };

            cmd.Parameters.Add("p_user_id", OracleDbType.Int32).Value = userId;
            cmd.Parameters.Add("p_city", OracleDbType.Varchar2, 100)
                           .Value = (object?)(m.NewCity?.Trim()) ?? DBNull.Value;
            cmd.Parameters.Add("p_stars", OracleDbType.Int32).Value = m.NewStars;
            cmd.Parameters.Add("p_text", OracleDbType.Varchar2, 1000)
                           .Value = m.NewText!.Trim();

            await cmd.ExecuteNonQueryAsync();

            TempData["Msg"] = "Děkujeme za vaše hodnocení!";
            return RedirectToAction(nameof(Index));
        }
    }
}
