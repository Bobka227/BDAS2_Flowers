using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.Domain;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers
{
    public class EventsController : Controller
    {
        private readonly IDbFactory _db;
        public EventsController(IDbFactory db) => _db = db;

        [HttpGet("/events/type/{id:int}")]
        public async Task<IActionResult> Type(int id)
        {
            EventType? et = null;

            await using var conn = await _db.CreateOpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT eventtypeid, eventname FROM event_type WHERE eventtypeid = :id";
            cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Int32, id, ParameterDirection.Input));

            await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            if (await r.ReadAsync())
            {
                et = new EventType
                {
                    EventTypeId = DbRead.GetInt32(r, 0),
                    EventName = r.GetString(1)
                };
            }
            if (et is null) return NotFound();

            var vm = new EventTypeViewModel
            {
                EventTypeId = et.EventTypeId,
                Name = et.EventName,
                Description = "Popis připravujeme. Vyplňte objednávku a my se vám ozveme."
            };
            return View(vm);
        }
    }

    public sealed class EventTypeViewModel
    {
        public int EventTypeId { get; init; }
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
    }
}
