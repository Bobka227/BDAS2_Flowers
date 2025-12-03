using BDAS2_Flowers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;

namespace BDAS2_Flowers.Controllers.AdminControllers;

[Authorize(Roles = "Admin")]
[Route("admin/pictures")]
public class AdminPicturesController : Controller
{
    private readonly IDbFactory _db;
    public AdminPicturesController(IDbFactory db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? qName, string? qExt, string? qProduct)
    {
        var rows = new List<(int PictureId, int ProductId, string Product, string FileName, string Ext, long SizeBytes, DateTime? Uploaded, DateTime? Modified)>();
        var products = new List<(int Id, string Name)>();
        var exts = new List<string>();

        await using var con = await _db.CreateOpenAsync();

        var sql = @"
            SELECT PICTUREID, PRODUCTID, PRODUCT, FILE_NAME, EXT, SIZE_BYTES, UPLOADED, MODIFIED
            FROM   VW_ADMIN_PICTURES
            WHERE  1=1";
        var cmd = con.CreateCommand();
        var oc = (OracleCommand)cmd;
        oc.BindByName = true;

        if (!string.IsNullOrWhiteSpace(qName))
        {
            sql += " AND UPPER(FILE_NAME) LIKE UPPER('%' || :qName || '%')";
            oc.Parameters.Add("qName", OracleDbType.Varchar2).Value = qName.Trim();
        }
        if (!string.IsNullOrWhiteSpace(qProduct))
        {
            sql += " AND UPPER(PRODUCT) LIKE UPPER('%' || :qProd || '%')";
            oc.Parameters.Add("qProd", OracleDbType.Varchar2).Value = qProduct.Trim();
        }
        if (!string.IsNullOrWhiteSpace(qExt))
        {
            sql += " AND UPPER(EXT) = UPPER(:qExt)";
            oc.Parameters.Add("qExt", OracleDbType.Varchar2).Value = qExt.Trim();
        }

        sql += " ORDER BY PICTUREID DESC";
        cmd.CommandText = sql;

        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                rows.Add((
                    r.GetInt32(0),
                    r.IsDBNull(1) ? 0 : r.GetInt32(1),
                    r.IsDBNull(2) ? "" : r.GetString(2),
                    r.IsDBNull(3) ? "" : r.GetString(3),
                    r.IsDBNull(4) ? "" : r.GetString(4),
                    r.IsDBNull(5) ? 0L : r.GetInt64(5),
                    r.IsDBNull(6) ? (DateTime?)null : r.GetDateTime(6),
                    r.IsDBNull(7) ? (DateTime?)null : r.GetDateTime(7)
                ));
            }
        }

        // TODO VIEW
        await using (var cmd2 = con.CreateCommand())
        {
            cmd2.CommandText = @"SELECT PRODUCTID, NAME
                                   FROM PRODUCT
                                  WHERE NAME NOT LIKE '~~ARCHIVED~~ %'
                               ORDER BY NAME";
            await using var r2 = await cmd2.ExecuteReaderAsync();
            while (await r2.ReadAsync()) products.Add((r2.GetInt32(0), r2.GetString(1)));
        }

        await using (var cmd3 = con.CreateCommand())
        {
            cmd3.CommandText = @"SELECT DISTINCT UPPER(EXTENTION) FROM PICTURE_FORMAT ORDER BY 1";
            await using var r3 = await cmd3.ExecuteReaderAsync();
            while (await r3.ReadAsync()) exts.Add(r3.GetString(0));
        }

        ViewBag.Products = products;
        ViewBag.Exts = exts;
        ViewBag.Filters = new { qName, qExt, qProduct };
        ViewData["Title"] = "Obrázky";
        return View("/Views/AdminPanel/Pictures/Index.cshtml", rows);
    }

    [HttpGet("{id:int}/content")]
    public async Task<IActionResult> Content(int id)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = con.CreateCommand();
        cmd.CommandText = @"SELECT CONTENT_BLOB, EXT FROM VW_ADMIN_PICTURES WHERE PICTUREID = :id";
        ((OracleCommand)cmd).BindByName = true;
        ((OracleCommand)cmd).Parameters.Add("id", OracleDbType.Int32).Value = id;

        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
        if (!await r.ReadAsync()) return NotFound();

        byte[] bytes;
        using (OracleBlob blob = r.GetOracleBlob(0))
        {
            bytes = new byte[blob.Length];
            _ = await blob.ReadAsync(bytes, 0, (int)blob.Length);
        }

        var ext = r.IsDBNull(1) ? "bin" : r.GetString(1);
        var mime = ext.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "gif" => "image/gif",
            "webp" => "image/webp",
            "bmp" => "image/bmp",
            _ => "application/octet-stream"
        };
        return File(bytes, mime);
    }

    [ValidateAntiForgeryToken]
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(int productId, string? name, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Msg"] = "Soubor nebyl vybrán.";
            return RedirectToAction(nameof(Index));
        }

        var fileName = string.IsNullOrWhiteSpace(name) ? file.FileName : name!.Trim();
        var ext = System.IO.Path.GetExtension(file.FileName)?.TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext)) ext = "bin";

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_SET_PRODUCT_PICTURE", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure, BindByName = true };

        cmd.Parameters.Add("p_product_id", OracleDbType.Int32).Value = productId;
        cmd.Parameters.Add("p_name", OracleDbType.Varchar2, 200).Value = fileName;
        cmd.Parameters.Add("p_ext", OracleDbType.Varchar2, 20).Value = ext;
        cmd.Parameters.Add("p_blob", OracleDbType.Blob).Value = bytes;
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";
        cmd.Parameters.Add("o_picture_id", OracleDbType.Int32).Direction = ParameterDirection.Output;

        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = "Obrázek uložen/aktualizován.";
        }
        catch (OracleException ex)
        {
            TempData["Msg"] = "Chyba při nahrávání obrázku: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_DELETE_PRODUCT_PICTURE", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure, BindByName = true };

        cmd.Parameters.Add("p_picture_id", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";

        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = "Obrázek odstraněn.";
        }
        catch (OracleException ex)
        {
            TempData["Msg"] = "Nelze odstranit obrázek: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{id:int}/update")]
    public async Task<IActionResult> UpdateMeta(int id, int productId, string name)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_PICTURE_UPDATE_META", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure, BindByName = true };

        cmd.Parameters.Add("p_picture_id", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_product_id", OracleDbType.Int32).Value = productId;
        cmd.Parameters.Add("p_name", OracleDbType.Varchar2, 200).Value = name?.Trim();
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";

        try { await cmd.ExecuteNonQueryAsync(); TempData["Msg"] = "Změny uloženy."; }
        catch (OracleException ex) { TempData["Msg"] = "Nelze uložit změny: " + ex.Message; }

        return RedirectToAction(nameof(Index), Request.Query.ToDictionary(k => k.Key, v => (object)v.Value.ToString()));
    }
}