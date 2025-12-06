using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.AdminModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers.AdminControllers
{
    /// <summary>
    /// Administrátorský controller pro správu slevových kupónů.
    /// Umožňuje jejich vyhledávání, vytváření, úpravu a mazání.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class AdminCouponsController : Controller
    {
        private readonly IDbFactory _db;

        /// <summary>
        /// Inicializuje novou instanci <see cref="AdminCouponsController"/> s továrnou databázových připojení.
        /// </summary>
        /// <param name="db">Implementace továrny pro vytváření otevřených databázových připojení.</param>
        public AdminCouponsController(IDbFactory db) => _db = db;

        /// <summary>
        /// Vrací identifikaci aktuálního uživatele (administrátora) pro auditní záznamy v databázi.
        /// </summary>
        private string CurrentActor =>
            User?.Identity?.Name ?? User?.FindFirst("email")?.Value ?? "UNKNOWN";

        /// <summary>
        /// Zobrazí seznam kupónů s možností filtrování podle kódu.
        /// </summary>
        /// <param name="q">Volitelný textový filtr, část kódu kupónu.</param>
        /// <returns>View s přehledem kupónů pro administraci.</returns>
        [HttpGet("/admin/coupons")]
        public async Task<IActionResult> Index(string? q)
        {
            var vm = new AdminCouponListVm
            {
                Query = q ?? string.Empty,
                Rows = new List<AdminCouponCodeVm>()
            };

            await using var con = await _db.CreateOpenAsync();

            var sql = @"
                SELECT ID, CODE, BONUS, DATEEXPIRY
                FROM VW_ADMIN_COUPONS
                WHERE (:q IS NULL OR UPPER(CODE) LIKE UPPER('%' || :q || '%'))
                ORDER BY CODE";

            await using var cmd = new OracleCommand(sql, con);
            cmd.BindByName = true;
            cmd.Parameters.Add("q", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(q) ? (object)DBNull.Value : q;

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                vm.Rows.Add(new AdminCouponCodeVm
                {
                    Id = r.GetInt32(0),
                    Code = r.GetString(1),
                    Bonus = r.GetDecimal(2),
                    DateExpiry = r.GetDateTime(3)
                });
            }

            return View("~/Views/AdminPanel/Coupons/Index.cshtml", vm);
        }

        /// <summary>
        /// Vytvoří nový slevový kupón pomocí uložené procedury <c>PRC_COUPON_CODE_CREATE</c>.
        /// </summary>
        /// <param name="code">Textový kód kupónu.</param>
        /// <param name="bonus">Hodnota bonusu (např. částka nebo procento).</param>
        /// <param name="dateExpiry">Datum, do kterého je kupón platný.</param>
        /// <returns>Přesměrování zpět na seznam kupónů.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string code, int bonus, DateTime? dateExpiry)
        {
            if (string.IsNullOrWhiteSpace(code) || bonus <= 0 || dateExpiry is null)
            {
                TempData["AdminError"] = "Vyplňte kód, bonus a datum platnosti.";
                return RedirectToAction(nameof(Index));
            }

            await using var con = await _db.CreateOpenAsync();
            await using var cmd = new OracleCommand("PRC_COUPON_CODE_CREATE", con)
            {
                CommandType = CommandType.StoredProcedure,
                BindByName = true
            };

            cmd.Parameters.Add("p_code", OracleDbType.Varchar2, 50).Value = code.Trim();
            cmd.Parameters.Add("p_bonus", OracleDbType.Int32).Value = bonus;
            cmd.Parameters.Add("p_date_expiry", OracleDbType.Date).Value = dateExpiry.Value;
            cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = CurrentActor;

            try
            {
                await cmd.ExecuteNonQueryAsync();
                TempData["AdminOk"] = "Kupón byl vytvořen.";
            }
            catch (OracleException ex)
            {
                TempData["AdminError"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Aktualizuje existující kupón pomocí uložené procedury <c>PRC_COUPON_CODE_UPDATE</c>.
        /// </summary>
        /// <param name="id">Identifikátor upravovaného kupónu.</param>
        /// <param name="code">Nový textový kód kupónu.</param>
        /// <param name="bonus">Nová hodnota bonusu kupónu.</param>
        /// <param name="dateExpiry">Nové datum expirace kupónu.</param>
        /// <returns>Přesměrování zpět na seznam kupónů.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, string code, int bonus, DateTime? dateExpiry)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(code) || bonus <= 0 || dateExpiry is null)
            {
                TempData["AdminError"] = "Neplatná data kupónu.";
                return RedirectToAction(nameof(Index));
            }

            await using var con = await _db.CreateOpenAsync();
            await using var cmd = new OracleCommand("PRC_COUPON_CODE_UPDATE", con)
            {
                CommandType = CommandType.StoredProcedure,
                BindByName = true
            };

            cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = id;
            cmd.Parameters.Add("p_code", OracleDbType.Varchar2, 50).Value = code.Trim();
            cmd.Parameters.Add("p_bonus", OracleDbType.Int32).Value = bonus;
            cmd.Parameters.Add("p_date_expiry", OracleDbType.Date).Value = dateExpiry.Value;
            cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = CurrentActor;

            try
            {
                await cmd.ExecuteNonQueryAsync();
                TempData["AdminOk"] = "Kupón byl aktualizován.";
            }
            catch (OracleException ex)
            {
                TempData["AdminError"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Smaže existující kupón pomocí uložené procedury <c>PRC_COUPON_CODE_DELETE</c>.
        /// </summary>
        /// <param name="id">Identifikátor mazáného kupónu.</param>
        /// <returns>Přesměrování zpět na seznam kupónů.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0)
            {
                TempData["AdminError"] = "Neplatné ID kupónu.";
                return RedirectToAction(nameof(Index));
            }

            await using var con = await _db.CreateOpenAsync();
            await using var cmd = new OracleCommand("PRC_COUPON_CODE_DELETE", con)
            {
                CommandType = CommandType.StoredProcedure,
                BindByName = true
            };

            cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = id;
            cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = CurrentActor;

            try
            {
                await cmd.ExecuteNonQueryAsync();
                TempData["AdminOk"] = "Kupón byl smazán.";
            }
            catch (OracleException ex)
            {
                TempData["AdminError"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
