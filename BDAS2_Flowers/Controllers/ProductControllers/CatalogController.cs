using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.ProductModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers.ProductControllers
{
    [Authorize]
    public class CatalogController : Controller
    {
        private readonly IDbFactory _db;
        public CatalogController(IDbFactory db) => _db = db;

        public async Task<IActionResult> Index(int page = 1, int? typeId = null)
        {
            const int pageSize = 8;
            var items = new List<ProductCardVm>();

            await using var conn = await _db.CreateOpenAsync();

            var categories = new List<(int Id, string Name)>();
            await using (var c1 = conn.CreateCommand())
            {
                c1.CommandText = @"
                    SELECT Id, Name
                      FROM VW_PRODUCT_TYPES
                     WHERE Name <> 'Návrh na akci'
                     ORDER BY Name";

                await using var r1 = await c1.ExecuteReaderAsync();
                while (await r1.ReadAsync())
                    categories.Add((DbRead.GetInt32(r1, 0), r1.GetString(1)));
            }
            ViewBag.Categories = categories;
            ViewBag.SelectedTypeId = typeId;

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                  SELECT ProductId, Title, Subtitle, PriceFrom, MainPicId, TypeId
                  FROM VW_CATALOG_PRODUCTS
                  /**where**/
                  ORDER BY Title
                  OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";

            if (typeId.HasValue)
            {
                cmd.CommandText = cmd.CommandText.Replace("/**where**/", "WHERE TypeId = :typeId");
                cmd.Parameters.Add(new OracleParameter("typeId", OracleDbType.Int32, typeId.Value, ParameterDirection.Input));
            }
            else
            {
                cmd.CommandText = cmd.CommandText.Replace("/**where**/", "");
            }

            cmd.Parameters.Add(new OracleParameter("skip", OracleDbType.Int32, (page - 1) * pageSize, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("take", OracleDbType.Int32, pageSize, ParameterDirection.Input));

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var picId = r.IsDBNull(4) ? (int?)null : DbRead.GetInt32(r, 4);
                items.Add(new ProductCardVm
                {
                    ProductId = DbRead.GetInt32(r, 0),
                    Title = r.GetString(1),
                    Subtitle = r.IsDBNull(2) ? null : r.GetString(2),
                    PriceFrom = Convert.ToDecimal(r.GetValue(3)),
                    ImageUrl = picId is int id ? $"/pictures/{id}" : "/img/placeholder.jpg"
                });
            }

            ViewBag.Page = page;
            ViewBag.HasNext = items.Count == pageSize;
            return View(items);
        }
    }
}