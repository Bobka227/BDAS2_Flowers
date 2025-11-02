using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DiagnosticsController : Controller
    {
        private readonly OracleConnectionStringBuilder _csb;
        public DiagnosticsController(OracleConnectionStringBuilder csb) => _csb = csb;

        [HttpGet("/diag")]
        public async Task<IActionResult> Summary()
        {
            var result = new
            {
                Db = "FAIL",
                Tables = new Dictionary<string, long>(),
                Sequences = new List<string>(),
                Triggers = new List<string>()
            };

            try
            {
                await using var conn = new OracleConnection(_csb.ConnectionString);
                await conn.OpenAsync();

                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT 'OK' FROM dual";
                    var ok = (string?)await cmd.ExecuteScalarAsync();
                    result = result with { Db = ok ?? "FAIL" };
                }

                var tableNames = new List<string>();
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT table_name FROM user_tables ORDER BY table_name";
                    await using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                        tableNames.Add(r.GetString(0)); 
                }

                foreach (var t in tableNames)
                {
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"SELECT COUNT(*) FROM \"{t}\"";
                    var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                    result.Tables[t] = count;
                }

                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT sequence_name FROM user_sequences ORDER BY sequence_name";
                    await using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                        result.Sequences.Add(r.GetString(0));
                }

                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT trigger_name FROM user_triggers ORDER BY trigger_name";
                    await using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                        result.Triggers.Add(r.GetString(0));
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                return Problem(ex.Message);
            }
        }
    }
}
