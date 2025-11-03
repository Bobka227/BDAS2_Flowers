using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Controllers
{
    public class HealthController : Controller
    {
        private readonly OracleConnectionStringBuilder _csb;
        public HealthController(OracleConnectionStringBuilder csb) => _csb = csb;

        [HttpGet("/db-check")]
        public async Task<IActionResult> DbCheck()
        {
            try
            {
                await using var conn = new OracleConnection(_csb.ConnectionString);
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 'OK' FROM dual";
                var ok = (string?)await cmd.ExecuteScalarAsync();

                return Ok(new { db = ok });
            }
            catch (Exception ex)
            {
                return Problem(ex.Message);
            }
        }
    }
}
