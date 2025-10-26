using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels;

namespace BDAS2_Flowers.Controllers
{
    public class EventsController : Controller
    {
        private readonly IDbFactory _db;
        private readonly IWebHostEnvironment _env;

        public EventsController(IDbFactory db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var list = new List<EventTypeCardVm>();

            await using var conn = await _db.CreateOpenAsync();
            await using var cmd = conn.CreateCommand();

            // Используй VW_EVENT_TYPES_OVERVIEW если создал её, иначе можно простую выборку из таблиц
            cmd.CommandText = @"
                SELECT et.EventTypeId AS Id,
                       et.EventName   AS Name,
                       NVL(cnt.Cnt,0) AS OrdersCount,
                       MaxDate        AS LastEventDate
                FROM EVENT_TYPE et
                LEFT JOIN (
                    SELECT EventTypeId, COUNT(*) Cnt, MAX(EventDate) MaxDate
                    FROM EVENT
                    GROUP BY EventTypeId
                ) cnt ON cnt.EventTypeId = et.EventTypeId
                ORDER BY et.EventName";

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var id = DbRead.GetInt32(r, 0);
                var name = r.GetString(1);
                var cnt = Convert.ToInt32(r.GetValue(2));
                var last = r.IsDBNull(3) ? (DateTime?)null : r.GetDateTime(3);

                list.Add(new EventTypeCardVm
                {
                    Id = id,
                    Name = name,
                    OrdersCount = cnt,
                    LastEventDate = last,
                    ImageUrl = ResolveImageUrl(name, id),
                    Subtitle = cnt > 0 ? $"Realizováno {cnt}×" +
                                (last.HasValue ? $", naposledy {last:dd.MM.yyyy}" : "")
                              : "Připravíme na míru"
                });
            }

            return View(list);
        }

        // Карта названий в имена файлов. Подстрой под свои типы.
        private static readonly Dictionary<string, string> _map = new(StringComparer.InvariantCultureIgnoreCase)
        {
            ["Svatba"] = "wedding.jpg",
            ["Narozeniny"] = "birthday.jpg",
            ["Promoce"] = "prom.jpg",
            ["Pohřeb"] = "funeral.jpg",
            ["Firemní akce"] = "corporate.jpg",
        };

        private string ResolveImageUrl(string name, int id)
        {
            // 1) явное соответствие по названию
            if (_map.TryGetValue(name, out var file) && FileExists(file))
                return "/img/events/" + file;

            // 2) слаг из названия (wedding-bouquet.jpg)
            var slug = Slugify(name);
            foreach (var ext in new[] { "jpg", "jpeg", "png", "webp" })
                if (FileExists($"{slug}.{ext}"))
                    return $"/img/events/{slug}.{ext}";

            // 3) файл по Id (1.jpg…)
            foreach (var ext in new[] { "jpg", "jpeg", "png", "webp" })
                if (FileExists($"{id}.{ext}"))
                    return $"/img/events/{id}.{ext}";

            // 4) плейсхолдер
            return "/img/events/generic.jpg";
        }

        private bool FileExists(string file) =>
            System.IO.File.Exists(Path.Combine(_env.WebRootPath, "img", "events", file));

        private static string Slugify(string s)
        {
            s = s.Trim().ToLowerInvariant();
            var repl = new Dictionary<char, char>
            {
                ['á'] = 'a',
                ['č'] = 'c',
                ['ď'] = 'd',
                ['é'] = 'e',
                ['ě'] = 'e',
                ['í'] = 'i',
                ['ň'] = 'n',
                ['ó'] = 'o',
                ['ř'] = 'r',
                ['š'] = 's',
                ['ť'] = 't',
                ['ú'] = 'u',
                ['ů'] = 'u',
                ['ý'] = 'y',
                ['ž'] = 'z'
            };
            var sb = new System.Text.StringBuilder();
            foreach (var ch in s)
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                else if (repl.TryGetValue(ch, out var r)) sb.Append(r);
                else if (char.IsWhiteSpace(ch) || ch == '/' || ch == '\\' || ch == '_') sb.Append('-');
            }
            var slug = sb.ToString();
            while (slug.Contains("--")) slug = slug.Replace("--", "-");
            return slug.Trim('-');
        }

        // Опционально: детальная страница конкретного типа события
        public IActionResult Detail(int id, string name)
        {
            ViewBag.EventName = name;
            return View();
        }
    }
}
