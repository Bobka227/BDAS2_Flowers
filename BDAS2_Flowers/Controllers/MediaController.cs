using System.Data;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Controllers
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

            byte[]? bytes = null;
            string? ext = null;

            const string sql = @"
                SELECT a.""CONTENT"", a.""EXTENTION""
                FROM ""ST72861"".""AVATAR"" a
                WHERE a.""USERID"" = :id";

            await using (var cmd = new OracleCommand(sql, con))
            {
                cmd.Parameters.Add(new OracleParameter("id", userId));
                await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);

                if (await r.ReadAsync())
                {
                    if (!r.IsDBNull(0))
                        bytes = (byte[])r.GetValue(0);

                    if (!r.IsDBNull(1))
                        ext = r.GetString(1);
                }
            }

            if (bytes == null)
            {
                return NotFound();
            }

            var contentType = (ext ?? "png").ToLower() switch
            {
                "jpg" or "jpeg" => "image/jpeg",
                "gif" => "image/gif",
                "webp" => "image/webp",
                _ => "image/png"
            };

            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";

            return File(bytes, contentType);
        }
    }
}
