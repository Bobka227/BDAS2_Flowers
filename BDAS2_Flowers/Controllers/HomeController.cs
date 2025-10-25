using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models;
using BDAS2_Flowers.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace BDAS2_Flowers.Controllers;

public class HomeController : Controller
{
    private readonly IDbFactory _db;
    public HomeController(IDbFactory db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var featured = new List<ProductCardVm>();

        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = conn.CreateCommand();

        // od nejvissi cenovky:
        //cmd.CommandText = @"
        //        SELECT ProductId, Title, Subtitle, PriceFrom, MainPicId
        //        FROM VW_CATALOG_PRODUCTS
        //        ORDER BY PriceFrom DESC
        //        FETCH FIRST 8 ROWS ONLY";

        // nahodne vybrane produkty:
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

        return View(featured);
    }
}