using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.ProductModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

/// <summary>
/// Administrátorský controller pro správu produktů v katalogu.
/// Umožňuje produkty filtrovat, doplňovat sklad, vytvářet, upravovat,
/// archivovat, mazat a pracovat s jejich obrázky.
/// </summary>
[Authorize(Roles = "Admin")]
[Route("admin/products")]
public class AdminProductsController : Controller
{
    private readonly IDbFactory _db;

    /// <summary>
    /// Inicializuje novou instanci <see cref="AdminProductsController"/> s továrnou databázových připojení.
    /// </summary>
    /// <param name="db">Továrna pro vytváření a otevírání databázových připojení.</param>
    public AdminProductsController(IDbFactory db) => _db = db;

    /// <summary>
    /// Zobrazí seznam produktů s možností filtrování podle názvu a typu produktu.
    /// </summary>
    /// <param name="q">Volitelný textový filtr – část názvu produktu.</param>
    /// <param name="typeId">Volitelný filtr podle identifikátoru typu produktu.</param>
    /// <returns>
    /// View s kolekcí řádků obsahujících základní údaje o produktech
    /// a seznam typů produktů ve <c>ViewBag.Types</c>.
    /// </returns>
    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, int? typeId)
    {
        var rows = new List<(int Id, string Title, string TypeName, int Stock, decimal Price, int TypeId)>();

        await using var conn = await _db.CreateOpenAsync();

        ViewBag.Types = await LoadTypesAsync(conn);

        ViewBag.Query = q;
        ViewBag.TypeId = typeId;

        var sql = @"
        SELECT ID, TITLE, TYPE_NAME, STOCK, PRICE, TYPE_ID
          FROM VW_ADMIN_PRODUCTS
         WHERE 1=1";

        var cmd = new OracleCommand { Connection = (OracleConnection)conn, BindByName = true };

        if (!string.IsNullOrWhiteSpace(q))
        {
            sql += " AND UPPER(TITLE) LIKE UPPER('%' || :q || '%')";
            cmd.Parameters.Add("q", OracleDbType.Varchar2).Value = q.Trim();
        }
        if (typeId.HasValue && typeId.Value > 0)
        {
            sql += " AND TYPE_ID = :tid";
            cmd.Parameters.Add("tid", OracleDbType.Int32).Value = typeId.Value;
        }

        sql += " ORDER BY TITLE";
        cmd.CommandText = sql;

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add((
                r.GetInt32(0),
                r.GetString(1),
                r.IsDBNull(2) ? "" : r.GetString(2),
                r.GetInt32(3),
                (decimal)r.GetDecimal(4),
                r.GetInt32(5)
            ));
        }

        return View(rows);
    }

    /// <summary>
    /// Vrátí částečný view s tabulkou produktů pro AJAX načítání (bez okolního layoutu).
    /// Filtrace se chová stejně jako v akci <see cref="Index(string?, int?)"/>.
    /// </summary>
    /// <param name="q">Volitelný textový filtr – část názvu produktu.</param>
    /// <param name="typeId">Volitelný filtr podle identifikátoru typu produktu.</param>
    /// <returns>Partial view <c>_ProductsTable</c> s kolekcí produktů.</returns>
    [HttpGet("list")]
    public async Task<IActionResult> List(string? q, int? typeId)
    {
        var rows = new List<(int Id, string Title, string TypeName, int Stock, decimal Price, int TypeId)>();

        await using var conn = await _db.CreateOpenAsync();
        var sql = @"SELECT ID, TITLE, TYPE_NAME, STOCK, PRICE, TYPE_ID
                  FROM VW_ADMIN_PRODUCTS
                 WHERE 1=1";
        var cmd = new OracleCommand { Connection = (OracleConnection)conn, BindByName = true };

        if (!string.IsNullOrWhiteSpace(q))
        {
            sql += " AND UPPER(TITLE) LIKE UPPER('%' || :q || '%')";
            cmd.Parameters.Add("q", OracleDbType.Varchar2).Value = q.Trim();
        }
        if (typeId.HasValue && typeId.Value > 0)
        {
            sql += " AND TYPE_ID = :tid";
            cmd.Parameters.Add("tid", OracleDbType.Int32).Value = typeId.Value;
        }

        sql += " ORDER BY TITLE";
        cmd.CommandText = sql;

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add((
                r.GetInt32(0),
                r.GetString(1),
                r.IsDBNull(2) ? "" : r.GetString(2),
                r.GetInt32(3),
                (decimal)r.GetDecimal(4),
                r.GetInt32(5)
            ));
        }

        return PartialView("_ProductsTable", rows);
    }

    /// <summary>
    /// Načte seznam typů produktů z pohledu <c>VW_PRODUCT_TYPES</c>.
    /// </summary>
    /// <param name="conn">Otevřené databázové připojení.</param>
    /// <returns>Kolekce dvojic (Id, název typu).</returns>
    private static async Task<IEnumerable<(int Id, string Name)>> LoadTypesAsync(IDbConnection conn)
    {
        var list = new List<(int, string)>();
        await using var cmd = new OracleCommand(@"SELECT ID, NAME FROM VW_PRODUCT_TYPES", (OracleConnection)conn);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            list.Add((rd.GetInt32(0), rd.GetString(1)));
        return list;
    }

    /// <summary>
    /// Hromadně upraví skladovou zásobu produktů určitého typu
    /// pomocí uložené procedury <c>PRC_RESTOCK_PRODUCTS</c>.
    /// </summary>
    /// <param name="typeId">Identifikátor typu produktu, jehož produkty se mají doplnit.</param>
    /// <param name="delta">Hodnota změny (kladná i záporná) skladového množství.</param>
    /// <returns>Přesměrování zpět na seznam produktů s informační zprávou.</returns>
    [HttpPost("restock")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restock(int typeId, int delta)
    {
        await using var conn = await _db.CreateOpenAsync();
        await using var tx = conn.BeginTransaction();
        try
        {
            await using (var cmd = new OracleCommand("PRC_RESTOCK_PRODUCTS", (OracleConnection)conn)
            { CommandType = CommandType.StoredProcedure, BindByName = true })
            {
                cmd.Parameters.Add("p_type_id", OracleDbType.Int32).Value = typeId;
                cmd.Parameters.Add("p_delta", OracleDbType.Int32).Value = delta;
                await cmd.ExecuteNonQueryAsync();
            }

            string typeName = "";
            await using (var cmd2 = new OracleCommand(@"SELECT NAME FROM VW_PRODUCT_TYPES WHERE ID = :id", (OracleConnection)conn))
            {
                cmd2.Parameters.Add(new OracleParameter("id", typeId));
                var o = await cmd2.ExecuteScalarAsync();
                typeName = Convert.ToString(o) ?? "";
            }

            await tx.CommitAsync();
            TempData["Msg"] = $"Restock OK: {typeName} (Δ={delta}).";
        }
        catch (OracleException ex)
        {
            await tx.RollbackAsync();
            TempData["Msg"] = $"Restock error: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Zobrazí formulář pro vytvoření nového produktu.
    /// </summary>
    /// <returns>View s prázdným modelem <see cref="ProductEditVm"/> a naplněnými typy produktů.</returns>
    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        await using var conn = await _db.CreateOpenAsync();
        var vm = new ProductEditVm { Types = await LoadTypesAsync(conn) };
        return View(vm);
    }

    /// <summary>
    /// Vytvoří nový produkt a volitelně k němu nahraje hlavní obrázek.
    /// </summary>
    /// <param name="vm">Model s údaji o novém produktu.</param>
    /// <param name="image">Volitelný obrázek produktu k nahrání.</param>
    /// <returns>
    /// Při úspěchu přesměruje na seznam produktů,
    /// při chybě nebo nevalidním modelu znovu zobrazí editační formulář.
    /// </returns>
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductEditVm vm, IFormFile? image)
    {
        if (!ModelState.IsValid)
        {
            await using var c0 = await _db.CreateOpenAsync();
            vm.Types = await LoadTypesAsync(c0);
            return View(vm);
        }

        await using var conn = await _db.CreateOpenAsync();
        await using var tx = conn.BeginTransaction();
        try
        {
            await using (var cmd = new OracleCommand("PRC_PRODUCT_CREATE", (OracleConnection)conn)
            { CommandType = CommandType.StoredProcedure, BindByName = true })
            {
                cmd.Parameters.Add("p_user_id", OracleDbType.Int32).Value = DBNull.Value;
                cmd.Parameters.Add("p_deliverymethodid", OracleDbType.Int32).Value = DBNull.Value;
                cmd.Parameters.Clear();
                cmd.Parameters.Add("p_name", OracleDbType.Varchar2).Value = vm.Name;
                cmd.Parameters.Add("p_price", OracleDbType.Decimal).Value = vm.Price;
                cmd.Parameters.Add("p_stock", OracleDbType.Int32).Value = vm.StockQuantity;
                cmd.Parameters.Add("p_type_id", OracleDbType.Int32).Value = vm.ProductTypeId;
                var oId = new OracleParameter("o_product_id", OracleDbType.Int32, ParameterDirection.Output);
                cmd.Parameters.Add(oId);
                await cmd.ExecuteNonQueryAsync();

                vm.ProductId = Convert.ToInt32(oId.Value.ToString());
            }

            if (image is { Length: > 0 })
            {
                var fileName = Path.GetFileName(image.FileName);
                var ext = Path.GetExtension(fileName).Trim('.').ToLowerInvariant();

                using var ms = new MemoryStream();
                await image.CopyToAsync(ms);
                var bytes = ms.ToArray();

                await using var cmd2 = new OracleCommand("PRC_SET_PRODUCT_PICTURE", (OracleConnection)conn)
                { CommandType = CommandType.StoredProcedure, BindByName = true };
                cmd2.Parameters.Add("p_product_id", OracleDbType.Int32).Value = vm.ProductId!.Value;
                cmd2.Parameters.Add("p_name", OracleDbType.Varchar2).Value = fileName;
                cmd2.Parameters.Add("p_ext", OracleDbType.Varchar2).Value = ext;
                cmd2.Parameters.Add("p_blob", OracleDbType.Blob).Value = bytes;
                cmd2.Parameters.Add("p_actor", OracleDbType.Varchar2).Value = User.Identity?.Name ?? "admin";
                var oPic = new OracleParameter("o_picture_id", OracleDbType.Int32, ParameterDirection.Output);
                cmd2.Parameters.Add(oPic);
                await cmd2.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            TempData["Msg"] = "Product created.";
            return RedirectToAction(nameof(Index));
        }
        catch (OracleException ex)
        {
            await tx.RollbackAsync();
            ModelState.AddModelError("", ex.Message);
            await using var c1 = await _db.CreateOpenAsync();
            vm.Types = await LoadTypesAsync(c1);
            return View(vm);
        }
    }

    /// <summary>
    /// Zobrazí formulář pro úpravu existujícího produktu.
    /// </summary>
    /// <param name="id">Identifikátor produktu.</param>
    /// <returns>
    /// View s naplněným modelem <see cref="ProductEditVm"/>,
    /// nebo <see cref="NotFoundResult"/>, pokud produkt neexistuje.
    /// </returns>
    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand(@"
        SELECT *
          FROM VW_PRODUCT_EDIT
         WHERE PRODUCTID = :id", (OracleConnection)conn);
        cmd.Parameters.Add(new OracleParameter("id", id));

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return NotFound();

        var vm = new ProductEditVm
        {
            ProductId = r.GetInt32(0),
            Name = r.GetString(1),
            Price = (decimal)r.GetDecimal(2),
            StockQuantity = r.GetInt32(3),
            ProductTypeId = r.GetInt32(4),
            MainPictureId = r.IsDBNull(5) ? (int?)null : r.GetInt32(5),
            Types = await LoadTypesAsync(conn)
        };
        return View(vm);
    }

    /// <summary>
    /// Uloží změny existujícího produktu a volitelně aktualizuje jeho obrázek.
    /// </summary>
    /// <param name="id">Identifikátor produktu z URL.</param>
    /// <param name="vm">Model s upravenými údaji produktu.</param>
    /// <param name="image">Volitelný nový obrázek produktu.</param>
    /// <returns>
    /// Při úspěchu přesměruje na seznam produktů,
    /// při chybě nebo nevalidním modelu znovu zobrazí editační formulář.
    /// </returns>
    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductEditVm vm, IFormFile? image)
    {
        if (!vm.ProductId.HasValue) vm.ProductId = id;

        if (!ModelState.IsValid)
        {
            await using var c0 = await _db.CreateOpenAsync();
            vm.Types = await LoadTypesAsync(c0);
            return View(vm);
        }

        await using var conn = await _db.CreateOpenAsync();
        await using var tx = conn.BeginTransaction();
        try
        {
            await using (var cmd = new OracleCommand("PRC_PRODUCT_UPDATE", (OracleConnection)conn)
            { CommandType = CommandType.StoredProcedure, BindByName = true })
            {
                cmd.Parameters.Add("p_product_id", OracleDbType.Int32).Value = vm.ProductId!.Value;
                cmd.Parameters.Add("p_name", OracleDbType.Varchar2).Value = vm.Name;
                cmd.Parameters.Add("p_price", OracleDbType.Decimal).Value = vm.Price;
                cmd.Parameters.Add("p_stock", OracleDbType.Int32).Value = vm.StockQuantity;
                cmd.Parameters.Add("p_type_id", OracleDbType.Int32).Value = vm.ProductTypeId;
                await cmd.ExecuteNonQueryAsync();
            }

            if (image is { Length: > 0 })
            {
                var fileName = Path.GetFileName(image.FileName);
                var ext = Path.GetExtension(fileName).Trim('.').ToLowerInvariant();
                using var ms = new MemoryStream();
                await image.CopyToAsync(ms);
                var bytes = ms.ToArray();

                await using var cmd2 = new OracleCommand("PRC_SET_PRODUCT_PICTURE", (OracleConnection)conn)
                { CommandType = CommandType.StoredProcedure, BindByName = true };
                cmd2.Parameters.Add("p_product_id", OracleDbType.Int32).Value = vm.ProductId!.Value;
                cmd2.Parameters.Add("p_name", OracleDbType.Varchar2).Value = fileName;
                cmd2.Parameters.Add("p_ext", OracleDbType.Varchar2).Value = ext;
                cmd2.Parameters.Add("p_blob", OracleDbType.Blob).Value = bytes;
                cmd2.Parameters.Add("p_actor", OracleDbType.Varchar2).Value = User.Identity?.Name ?? "admin";
                var oPic = new OracleParameter("o_picture_id", OracleDbType.Int32, ParameterDirection.Output);
                cmd2.Parameters.Add(oPic);
                await cmd2.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            TempData["Msg"] = "Product updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (OracleException ex)
        {
            await tx.RollbackAsync();
            ModelState.AddModelError("", ex.Message);
            await using var c1 = await _db.CreateOpenAsync();
            vm.Types = await LoadTypesAsync(c1);
            return View(vm);
        }
    }

    /// <summary>
    /// Archivuje (skryje) produkt z katalogu pomocí uložené procedury <c>PRC_PRODUCT_HIDE</c>.
    /// </summary>
    /// <param name="id">Identifikátor produktu.</param>
    /// <returns>Přesměrování zpět na seznam produktů s informační zprávou.</returns>
    [HttpPost("{id:int}/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        await using var conn = await _db.CreateOpenAsync();
        await using var tx = conn.BeginTransaction();
        try
        {
            await using var cmd = new OracleCommand("PRC_PRODUCT_HIDE", (OracleConnection)conn)
            { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_product_id", OracleDbType.Int32, id, ParameterDirection.Input);
            await cmd.ExecuteNonQueryAsync();
            await tx.CommitAsync();
            TempData["Msg"] = "Product archived (hidden from catalog).";
        }
        catch (OracleException ex)
        {
            await tx.RollbackAsync();
            TempData["Msg"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Obnoví (odarchivuje) produkt v katalogu pomocí uložené procedury <c>PRC_PRODUCT_UNHIDE</c>.
    /// </summary>
    /// <param name="id">Identifikátor produktu.</param>
    /// <returns>Přesměrování zpět na seznam produktů s informační zprávou.</returns>
    [HttpPost("{id:int}/unarchive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unarchive(int id)
    {
        await using var conn = await _db.CreateOpenAsync();
        await using var tx = conn.BeginTransaction();
        try
        {
            await using var cmd = new OracleCommand("PRC_PRODUCT_UNHIDE", (OracleConnection)conn)
            { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_product_id", OracleDbType.Int32, id, ParameterDirection.Input);

            await cmd.ExecuteNonQueryAsync();
            await tx.CommitAsync();
            TempData["Msg"] = "Product unarchived (visible in catalog).";
        }
        catch (OracleException ex)
        {
            await tx.RollbackAsync();
            TempData["Msg"] = "Cannot unarchive: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Trvale smaže produkt pomocí uložené procedury <c>PRC_PRODUCT_DELETE</c>.
    /// Pokud je produkt již použit v objednávkách, smaže se neprovede
    /// a zobrazí se informace o nutnosti produkt pouze archivovat.
    /// </summary>
    /// <param name="id">Identifikátor mazáného produktu.</param>
    /// <returns>Přesměrování zpět na seznam produktů.</returns>
    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await using var conn = await _db.CreateOpenAsync();
        await using var tx = conn.BeginTransaction();
        try
        {
            await using var cmd = new OracleCommand("PRC_PRODUCT_DELETE", (OracleConnection)conn)
            { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_product_id", OracleDbType.Int32, id, ParameterDirection.Input);

            await cmd.ExecuteNonQueryAsync();
            await tx.CommitAsync();
            TempData["Msg"] = "Product deleted.";
        }
        catch (OracleException ex) when (ex.Number == 20056 || ex.Number == -20056)
        {
            await tx.RollbackAsync();
            TempData["Msg"] = "Nelze odstranit: produkt už je v objednávkách. Klikni na „Archivovat“, aby se v katalogu nezobrazoval.";
            return RedirectToAction(nameof(Index));
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Smaže konkrétní obrázek produktu pomocí uložené procedury <c>PRC_DELETE_PRODUCT_PICTURE</c>.
    /// </summary>
    /// <param name="productId">Identifikátor produktu, ke kterému obrázek patří.</param>
    /// <param name="picId">Identifikátor mazáného obrázku.</param>
    /// <returns>
    /// Přesměrování zpět na editační stránku produktu
    /// s informační zprávou o výsledku operace.
    /// </returns>
    [HttpPost("{productId:int}/picture/{picId:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePicture(int productId, int picId)
    {
        await using var conn = await _db.CreateOpenAsync();
        await using var tx = conn.BeginTransaction();
        try
        {
            await using var cmd = new OracleCommand("PRC_DELETE_PRODUCT_PICTURE", (OracleConnection)conn)
            { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add("p_picture_id", OracleDbType.Int32).Value = picId;
            cmd.Parameters.Add("p_actor", OracleDbType.Varchar2).Value = User.Identity?.Name ?? "admin";
            await cmd.ExecuteNonQueryAsync();

            await tx.CommitAsync();
            TempData["Msg"] = "Picture deleted.";
        }
        catch (OracleException ex)
        {
            await tx.RollbackAsync();
            TempData["Msg"] = "Cannot delete picture: " + ex.Message;
        }
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

}
