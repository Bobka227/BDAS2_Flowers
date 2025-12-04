using System.Data;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Controllers.MediaControllers
{
    public class MediaController : Controller
    {
        private readonly IConfiguration _cfg;
        public MediaController(IConfiguration cfg) => _cfg = cfg;

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
