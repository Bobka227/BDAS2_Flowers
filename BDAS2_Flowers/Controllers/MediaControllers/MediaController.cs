using System.Data;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Controllers.MediaControllers
{
    /// <summary>
    /// Controller pro obsluhu binárních médií uložených v databázi (avatary uživatelů, produktové obrázky).
    /// Vrací obrázky jako HTTP odpověď s odpovídajícím MIME typem.
    /// </summary>
    public class MediaController : Controller
    {
        private readonly IConfiguration _cfg;

        /// <summary>
        /// Inicializuje novou instanci <see cref="MediaController"/> s přístupem ke konfiguraci aplikace.
        /// </summary>
        /// <param name="cfg">Konfigurace aplikace používaná pro načtení connection stringu k Oracle databázi.</param>
        public MediaController(IConfiguration cfg) => _cfg = cfg;

        /// <summary>
        /// Vrátí avatar uživatele jako obrázek načtený z pohledu <c>VW_MEDIA_AVATAR</c>.
        /// Pokud avatar neexistuje, vrací HTTP 404.
        /// </summary>
        /// <param name="userId">Identifikátor uživatele, jehož avatar se má načíst.</param>
        /// <returns>
        /// Binární obsah obrázku s odpovídajícím MIME typem,
        /// nebo <see cref="NotFoundResult"/>, pokud avatar neexistuje.
        /// </returns>
        [HttpGet("/media/avatar/{userId:int}")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Avatar(int userId)
        {
            await using var con = new OracleConnection(_cfg.GetConnectionString("Oracle"));
            await con.OpenAsync();

            byte[]? bytes = null; string? ext = null;

            const string sql = @"SELECT AVATAR_BLOB, EXT
                           FROM VW_MEDIA_AVATAR
                           WHERE USERID = :id";

            await using (var cmd = new OracleCommand(sql, con))
            {
                cmd.Parameters.Add(new OracleParameter("id", userId));
                await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
                if (await r.ReadAsync())
                {
                    if (!r.IsDBNull(0)) bytes = (byte[])r.GetValue(0);
                    if (!r.IsDBNull(1)) ext = r.GetString(1);
                }
            }
            if (bytes == null) return NotFound();

            var contentType = (ext ?? "png").ToLower() switch
            {
                "jpg" or "jpeg" => "image/jpeg",
                "gif" => "image/gif",
                "webp" => "image/webp",
                _ => "image/png"
            };
            return File(bytes, contentType);
        }

        /// <summary>
        /// Vrátí hlavní obrázek produktu jako binární obsah.
        /// Data čte z pohledu <c>VW_MEDIA_PRODUCT_MAIN</c>.
        /// </summary>
        /// <param name="productId">Identifikátor produktu, pro který se má hlavní obrázek načíst.</param>
        /// <returns>
        /// Binární obsah hlavního obrázku produktu s odpovídajícím MIME typem,
        /// nebo <see cref="NotFoundResult"/>, pokud obrázek pro daný produkt neexistuje.
        /// </returns>
        [HttpGet("/media/product/by-product/{productId:int}")]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any, NoStore = false)]
        public async Task<IActionResult> ProductMainImage(int productId)
        {
            await using var con = new OracleConnection(_cfg.GetConnectionString("Oracle"));
            await con.OpenAsync();

            byte[]? bytes = null; string? ext = null;

            const string sql = @"SELECT PIC_BLOB, EXT
                           FROM VW_MEDIA_PRODUCT_MAIN
                           WHERE PRODUCTID = :pid";

            await using (var cmd = new OracleCommand(sql, con))
            {
                cmd.Parameters.Add(new OracleParameter("pid", productId));
                await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
                if (await r.ReadAsync())
                {
                    if (!r.IsDBNull(0)) bytes = (byte[])r.GetValue(0);
                    if (!r.IsDBNull(1)) ext = r.GetString(1);
                }
            }
            if (bytes == null) return NotFound();

            var contentType = (ext ?? "png").ToLower() switch
            {
                "jpg" or "jpeg" => "image/jpeg",
                "gif" => "image/gif",
                "webp" => "image/webp",
                _ => "image/png"
            };
            return File(bytes, contentType);
        }
    }
}
