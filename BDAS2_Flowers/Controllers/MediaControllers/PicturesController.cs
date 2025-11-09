using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using BDAS2_Flowers.Data;
using System.Data;

namespace BDAS2_Flowers.Controllers.MediaControllers
{
    public class PicturesController : Controller
    {
        private readonly IDbFactory _db;
        public PicturesController(IDbFactory db) => _db = db;

        [HttpGet("/pictures/{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            await using var conn = await _db.CreateOpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT p.Picture, pf.Extention
                                FROM PICTURE p JOIN PICTURE_FORMAT pf ON pf.FormatId=p.FormatId
                                WHERE p.PictureId=:id";
            cmd.Parameters.Add(new OracleParameter("id", id));
            await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
            if (!await r.ReadAsync()) return NotFound();

            using var ms = new MemoryStream();
            await r.GetStream(0).CopyToAsync(ms);
            var bytes = ms.ToArray();
            var ext = r.GetString(1).Trim('.').ToLowerInvariant();
            var mime = ext switch { "jpg" or "jpeg" => "image/jpeg", "png" => "image/png", "webp" => "image/webp", "gif" => "image/gif", _ => "application/octet-stream" };
            Response.Headers["Cache-Control"] = "public,max-age=86400";
            return File(bytes, mime);
        }
    }
}
