using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using BDAS2_Flowers.Models.ViewModels.OrderModels;
using System.Security.Claims;

[Authorize]
[Route("cart")]
public class CartController : Controller
{
    private readonly IConfiguration _cfg;

    /// <summary>
    /// Klíč pro uložení košíku v uživatelské session.
    /// Pro přihlášeného uživatele používá formát <c>CART_USER_{userId}</c>,
    /// pro anonymního uživatele <c>CART_ANON</c>.
    /// </summary>
    private string CartKey
    {
        get
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
                return $"CART_USER_{userId}";

            return "CART_ANON";
        }
    }

    /// <summary>
    /// Inicializuje novou instanci <see cref="CartController"/> s konfigurací aplikace.
    /// </summary>
    /// <param name="cfg">Konfigurace aplikace (používá se zejména connection string k databázi).</param>
    public CartController(IConfiguration cfg) => _cfg = cfg;

    /// <summary>
    /// Doplní položkám košíku chybějící data (název a cenu) z databázového pohledu <c>VW_PRODUCT_EDIT</c>
    /// a uloží aktualizovaný košík zpět do session.
    /// </summary>
    /// <param name="cart">Model košíku, ve kterém mohou být některé položky neúplné.</param>
    private async Task HydrateAsync(CartVm cart)
    {
        var need = cart.Items.Where(i => i.UnitPrice <= 0m || string.IsNullOrWhiteSpace(i.Title)).ToList();
        if (need.Count == 0) return;

        await using var con = new OracleConnection(_cfg.GetConnectionString("Oracle"));
        await con.OpenAsync();

        foreach (var it in need)
        {
            await using var cmd = new OracleCommand(
                @"SELECT NAME, PRICE
                    FROM VW_PRODUCT_EDIT
                   WHERE PRODUCTID = :id", con);
            cmd.Parameters.Add("id", it.ProductId);
            await using var rd = await cmd.ExecuteReaderAsync();
            if (await rd.ReadAsync())
            {
                it.Title = rd.GetString(0);
                it.UnitPrice = (decimal)rd.GetDecimal(1);
            }

        }
        HttpContext.Session.SetJson(CartKey, cart);
    }

    /// <summary>
    /// Zobrazí obsah košíku aktuálního uživatele.
    /// V případě potřeby doplní chybějící informace o produktech z databáze.
    /// </summary>
    /// <returns>View s modelem <see cref="CartVm"/>.</returns>
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var cart = HttpContext.Session.GetJson<CartVm>(CartKey) ?? new CartVm();
        await HydrateAsync(cart);
        return View(cart);
    }

    /// <summary>
    /// Přidá produkt do košíku nebo navýší množství, pokud už v košíku existuje.
    /// Podporuje jak běžný POST, tak AJAX volání (vrací JSON odpověď).
    /// </summary>
    /// <param name="productId">Identifikátor přidávaného produktu.</param>
    /// <param name="quantity">Počet kusů, které se mají přidat (výchozí 1).</param>
    /// <returns>
    /// Při AJAX volání JSON s výsledkem a počtem položek v košíku,
    /// jinak přesměrování zpět na předchozí stránku nebo do katalogu.
    /// </returns>
    [ValidateAntiForgeryToken]
    [HttpPost("add")]
    public async Task<IActionResult> Add(int productId, int quantity = 1)
    {
        bool isAjax = string.Equals(
            Request.Headers["X-Requested-With"],
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);

        if (productId <= 0 || quantity <= 0)
        {
            if (isAjax)
                return BadRequest(new { ok = false, error = "Neplatný produkt." });

            return Redirect(Request.Headers["Referer"].ToString() ?? "/catalog");
        }

        await using var con = new OracleConnection(_cfg.GetConnectionString("Oracle"));
        await con.OpenAsync();

        string title;
        decimal price;

        await using (var cmd = new OracleCommand(
            @"SELECT NAME, PRICE
                FROM VW_PRODUCT_EDIT
               WHERE PRODUCTID = :id", con))
        {
            cmd.Parameters.Add("id", productId);
            await using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync())
            {
                if (isAjax)
                    return NotFound(new { ok = false, error = "Produkt nebyl nalezen." });

                TempData["Error"] = "Produkt nebyl nalezen.";
                return Redirect(Request.Headers["Referer"].ToString() ?? "/catalog");
            }

            title = rd.GetString(0);
            price = (decimal)rd.GetDecimal(1);
        }


        var cart = HttpContext.Session.GetJson<CartVm>(CartKey) ?? new CartVm();
        var line = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (line == null)
            cart.Items.Add(new CartItemVm
            {
                ProductId = productId,
                Title = title,
                UnitPrice = price,
                Quantity = quantity
            });
        else
            line.Quantity += quantity;

        HttpContext.Session.SetJson(CartKey, cart);
        var totalCount = cart.Items.Sum(i => i.Quantity);

        if (isAjax)
            return Json(new { ok = true, count = totalCount });

        return Redirect(Request.Headers["Referer"].ToString() ?? "/catalog");
    }

    /// <summary>
    /// Zvýší množství daného produktu v košíku o 1.
    /// </summary>
    /// <param name="productId">Identifikátor produktu, jehož množství se má navýšit.</param>
    /// <returns>Přesměrování na stránku košíku.</returns>
    [ValidateAntiForgeryToken]
    [HttpPost("inc")]
    public IActionResult Inc(int productId)
    {
        var cart = HttpContext.Session.GetJson<CartVm>(CartKey) ?? new CartVm();
        var it = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (it != null) it.Quantity++;
        HttpContext.Session.SetJson(CartKey, cart);
        return Redirect("/cart");
    }

    /// <summary>
    /// Sníží množství daného produktu v košíku o 1.
    /// Pokud množství klesne na nulu nebo méně, položka se z košíku odstraní.
    /// </summary>
    /// <param name="productId">Identifikátor produktu, jehož množství se má snížit.</param>
    /// <returns>Přesměrování na stránku košíku.</returns>
    [ValidateAntiForgeryToken]
    [HttpPost("dec")]
    public IActionResult Dec(int productId)
    {
        var cart = HttpContext.Session.GetJson<CartVm>(CartKey) ?? new CartVm();
        var it = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (it != null)
        {
            it.Quantity--;
            if (it.Quantity <= 0) cart.Items.Remove(it);
        }
        HttpContext.Session.SetJson(CartKey, cart);
        return Redirect("/cart");
    }

    /// <summary>
    /// Odstraní konkrétní produkt z košíku (bez ohledu na aktuální množství).
    /// </summary>
    /// <param name="productId">Identifikátor produktu, který se má z košíku odstranit.</param>
    /// <returns>Přesměrování na stránku košíku.</returns>
    [ValidateAntiForgeryToken]
    [HttpPost("remove")]
    public IActionResult Remove(int productId)
    {
        var cart = HttpContext.Session.GetJson<CartVm>(CartKey) ?? new CartVm();
        cart.Items.RemoveAll(i => i.ProductId == productId);
        HttpContext.Session.SetJson(CartKey, cart);
        return Redirect("/cart");
    }

    /// <summary>
    /// Vyprázdní celý košík aktuálního uživatele odstraněním dat ze session.
    /// </summary>
    /// <returns>Přesměrování na stránku košíku.</returns>
    [ValidateAntiForgeryToken]
    [HttpPost("clear")]
    public IActionResult Clear()
    {
        HttpContext.Session.Remove(CartKey);
        return Redirect("/cart");
    }
}
