using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.Domain;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

/// <summary>
/// View komponenta pro zobrazení nabídky typů událostí (např. v menu nebo rozbalovacím seznamu).
/// </summary>
public class EventsMenuViewComponent : ViewComponent
{
    private readonly IDbFactory _db;

    /// <summary>
    /// Vytvoří novou instanci view komponenty pro menu událostí.
    /// </summary>
    /// <param name="db">Fabrika pro vytváření a otevření Oracle připojení.</param>
    public EventsMenuViewComponent(IDbFactory _db) => this._db = _db;

    /// <summary>
    /// Načte seznam typů událostí z databáze a vrátí view s kolekcí <see cref="EventType"/>.
    /// </summary>
    /// <param name="onlyUsed">
    /// Pokud je <c>true</c>, načtou se pouze typy událostí, které jsou reálně použity
    /// (z pohledu <c>VW_EVENT_TYPES_USED</c>), jinak se načtou všechny typy z tabulky <c>EVENT_TYPE</c>.
    /// </param>
    /// <returns>
    /// Asynchronní výsledek vykreslení view komponenty s kolekcí typů událostí.
    /// </returns>
    public async Task<IViewComponentResult> InvokeAsync(bool onlyUsed = false)
    {
        var items = new List<EventType>();

        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = onlyUsed
          ? @"SELECT eventtypeid, eventname FROM VW_EVENT_TYPES_USED ORDER BY eventname"
          : @"SELECT eventtypeid, eventname FROM event_type ORDER BY eventname";

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
