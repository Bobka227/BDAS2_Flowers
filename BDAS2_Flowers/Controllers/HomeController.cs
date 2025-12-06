using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.ProductModels;
using Microsoft.AspNetCore.Mvc;

namespace BDAS2_Flowers.Controllers
{
    /// <summary>
    /// Řadič pro veřejnou část webu – úvodní stránku a základní informační stránky.
    /// </summary>
    public class HomeController : Controller
    {
        private readonly IDbFactory _db;

        /// <summary>
        /// Vytvoří novou instanci řadiče <see cref="HomeController"/>.
        /// </summary>
        /// <param name="db">
        /// Továrna na databázová připojení používaná pro načítání dat (produkty, prodejny).
        /// </param>
        public HomeController(IDbFactory db) => _db = db;

        /// <summary>
        /// Zobrazí úvodní stránku e-shopu s náhodně vybranými doporučenými produkty.
        /// </summary>
        /// <remarks>
        /// Z databázového pohledu <c>VW_CATALOG_PRODUCTS</c> vybere náhodně až čtyři produkty,
        /// které jsou zobrazeny jako „doporučené“ na homepage. Obrázky produktů jsou načteny
        /// přes kontroler obrázků, případně je použit výchozí placeholder.
        /// </remarks>
        /// <returns>
        /// Pohled úvodní stránky s kolekcí <see cref="ProductCardVm"/> jako modelem.
        /// </returns>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var featured = new List<ProductCardVm>();

            await using var conn = await _db.CreateOpenAsync();
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
            SELECT ProductId, Title, Subtitle, PriceFrom, MainPicId
            FROM (
              SELECT ProductId, Title, Subtitle, PriceFrom, MainPicId
              FROM VW_CATALOG_PRODUCTS
              ORDER BY DBMS_RANDOM.VALUE
            )
            WHERE ROWNUM <= 4";

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var picId = r.IsDBNull(4) ? (int?)null : DbRead.GetInt32(r, 4);
                featured.Add(new ProductCardVm
                {
                    ProductId = DbRead.GetInt32(r, 0),
                    Title = r.GetString(1),
                    Subtitle = r.IsDBNull(2) ? null : r.GetString(2),
                    PriceFrom = Convert.ToDecimal(r.GetValue(3)),
                    ImageUrl = picId is int id ? $"/pictures/{id}" : "/img/placeholder.jpg"
                });
            }

            // Na úvodní stránce se u produktů neschovává tlačítko „Přidat do košíku“.
            ViewBag.HideAdd = true;

            return View(featured);
        }

        /// <summary>
        /// Zobrazí stránku s kontaktními údaji jednotlivých prodejen.
        /// </summary>
        /// <remarks>
        /// Načte seznam prodejen z pohledu <c>VW_SHOPS</c> a k nim přiřadí telefonní čísla
        /// z tabulky <c>FLOWER_SHOP</c>. Výsledkem je seznam prodejen s názvem a telefonem,
        /// předaný do informačního pohledu <c>Contacts.cshtml</c>.
        /// </remarks>
        /// <returns>
        /// Pohled s přehledem prodejen a jejich telefonních kontaktů.
        /// </returns>
        [HttpGet]
        public async Task<IActionResult> Contact()
        {
            var rows = new List<(int Id, string Name, string? Phone)>();

            await using var con = await _db.CreateOpenAsync();
            await using var cmd = con.CreateCommand();
            cmd.CommandText = @"
        SELECT v.ID, v.NAME, fs.PHONE
          FROM ST72861.VW_SHOPS v
          LEFT JOIN ST72861.FLOWER_SHOP fs ON fs.SHOPID = v.ID
         ORDER BY v.NAME";

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                rows.Add((Convert.ToInt32(r.GetValue(0)),
                          r.GetString(1),
                          r.IsDBNull(2) ? null : r.GetValue(2)?.ToString()));

            return View("~/Views/Shared/Components/InfoPages/Contacts.cshtml", rows);
        }

        /// <summary>
        /// Zobrazí statickou informační stránku s recenzemi.
        /// </summary>
        /// <remarks>
        /// Tato akce pouze vrací view <c>Recenze.cshtml</c>. Samotné načítání a ukládání
        /// recenzí je řešeno v samostatném řadiči pro recenze.
        /// </remarks>
        /// <returns>
        /// Pohled s obsahem stránky „Recenze“.
        /// </returns>
        [HttpGet]
        public IActionResult Recenze()
            => View("~/Views/Shared/Components/InfoPages/Recenze.cshtml");
    }
}
