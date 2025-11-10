using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.ProductModels;
using Microsoft.AspNetCore.Mvc;

namespace BDAS2_Flowers.Controllers;

public class HomeController : Controller
{
    private readonly IDbFactory _db;
    public HomeController(IDbFactory db) => _db = db;

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
            WHERE ROWNUM <= 8";

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

        ViewBag.HideAdd = true;

        return View(featured);
    }

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

    [HttpGet]
    public IActionResult Recenze()
    => View("~/Views/Shared/Components/InfoPages/Recenze.cshtml");
}