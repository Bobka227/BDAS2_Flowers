using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.Domain;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

public class EventsMenuViewComponent : ViewComponent
{
    private readonly IDbFactory _db;
    public EventsMenuViewComponent(IDbFactory db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync(bool onlyUsed = false)
    {
        var items = new List<EventType>();

        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = onlyUsed
            ? @"SELECT et.eventtypeid, et.eventname
                  FROM event_type et
                 WHERE EXISTS (SELECT 1 FROM event e WHERE e.eventtypeid = et.eventtypeid)
                 ORDER BY et.eventname"
            : @"SELECT eventtypeid, eventname
                  FROM event_type
                 ORDER BY eventname";

        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        while (await r.ReadAsync())
        {
            items.Add(new EventType
            {
                EventTypeId = DbRead.GetInt32(r, 0),
                EventName = r.GetString(1)
            });
        }

        return View(items);
    }
}
