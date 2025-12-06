using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using BDAS2_Flowers.Data;
using System.Data;

namespace BDAS2_Flowers.Controllers.MediaControllers
{
    /// <summary>
    /// Controller pro poskytování obrázků uložených v databázi.
    /// Na základě ID obrázku vrací jeho binární obsah s odpovídajícím MIME typem.
    /// </summary>
    public class PicturesController : Controller
    {
        private readonly IDbFactory _db;

        /// <summary>
        /// Inicializuje novou instanci <see cref="PicturesController"/> s továrnou databázových připojení.
        /// </summary>
        /// <param name="db">Továrna pro vytváření a otevírání databázových připojení.</param>
        public PicturesController(IDbFactory db) => _db = db;

        /// <summary>
        /// Vrátí obsah obrázku podle jeho ID z pohledu <c>VW_ADMIN_PICTURES</c>.
        /// Obrázek je čten jako BLOB a vrácen s detekovaným MIME typem.
        /// </summary>
        /// <param name="id">Identifikátor obrázku (PICTUREID).</param>
        /// <returns>
        /// Binární obsah obrázku s odpovídajícím MIME typem,
        /// nebo <see cref="NotFoundResult"/>, pokud obrázek neexistuje.
        /// </returns>
        [HttpGet("/pictures/{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            await using var conn = await _db.CreateOpenAsync();
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
            SELECT CONTENT_BLOB, EXT
              FROM VW_ADMIN_PICTURES
             WHERE PICTUREID = :id";

            cmd.Parameters.Add(new OracleParameter("id", id));

            await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
            if (!await r.ReadAsync())
                return NotFound();

            using var ms = new MemoryStream();
            await r.GetStream(0).CopyToAsync(ms);
            var bytes = ms.ToArray();

            var ext = r.GetString(1).Trim('.').ToLowerInvariant();
            var mime = ext switch
            {
                "jpg" or "jpeg" => "image/jpeg",
                "png" => "image/png",
                "webp" => "image/webp",
                "gif" => "image/gif",
                _ => "application/octet-stream"
            };

            Response.Headers["Cache-Control"] = "public,max-age=86400";
            return File(bytes, mime);
        }
    }
}
