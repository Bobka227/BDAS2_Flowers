using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using BDAS2_Flowers.Models.ViewModels;

[Authorize]
[Route("cart")]
public class CartController : Controller
{
    private readonly IConfiguration _cfg;
    private const string CartKey = "CART";
    public CartController(IConfiguration cfg) => _cfg = cfg;

    private async Task HydrateAsync(CartVm cart)
    {
        var need = cart.Items.Where(i => i.UnitPrice <= 0m || string.IsNullOrWhiteSpace(i.Title)).ToList();
        if (need.Count == 0) return;

        await using var con = new OracleConnection(_cfg.GetConnectionString("Oracle"));
        await con.OpenAsync();

        foreach (var it in need)
        {
            await using var cmd = new OracleCommand(
                @"SELECT Name, CAST(Price AS NUMBER(10,2)) 
                    FROM PRODUCT 
                   WHERE ProductId = :id", con);
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

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var cart = HttpContext.Session.GetJson<CartVm>(CartKey) ?? new CartVm();
        await HydrateAsync(cart);
        return View(cart);
    }

    [ValidateAntiForgeryToken]
    [HttpPost("add")]
    public async Task<IActionResult> Add(int productId, int quantity = 1)
    {
        if (productId <= 0 || quantity <= 0)
            return Redirect(Request.Headers["Referer"].ToString() ?? "/catalog");

        await using var con = new OracleConnection(_cfg.GetConnectionString("Oracle"));
        await con.OpenAsync();

        string title;
        decimal price;
        await using (var cmd = new OracleCommand(
            @"SELECT Name, CAST(Price AS NUMBER(10,2)) 
                FROM PRODUCT 
               WHERE ProductId = :id", con))
        {
            cmd.Parameters.Add("id", productId);
            await using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync())
            {
                TempData["Error"] = "Produkt nebyl nalezen.";
                return Redirect(Request.Headers["Referer"].ToString() ?? "/catalog");
            }
            title = rd.GetString(0);
            price = (decimal)rd.GetDecimal(1);
        }

        var cart = HttpContext.Session.GetJson<CartVm>(CartKey) ?? new CartVm();
        var line = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (line == null)
            cart.Items.Add(new CartItemVm { ProductId = productId, Title = title, UnitPrice = price, Quantity = quantity });
        else
            line.Quantity += quantity;

        HttpContext.Session.SetJson(CartKey, cart);
        return Redirect(Request.Headers["Referer"].ToString() ?? "/catalog");
    }

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

    [ValidateAntiForgeryToken]
    [HttpPost("remove")]
    public IActionResult Remove(int productId)
    {
        var cart = HttpContext.Session.GetJson<CartVm>(CartKey) ?? new CartVm();
        cart.Items.RemoveAll(i => i.ProductId == productId);
        HttpContext.Session.SetJson(CartKey, cart);
        return Redirect("/cart");
    }

    [ValidateAntiForgeryToken]
    [HttpPost("clear")]
    public IActionResult Clear()
    {
        HttpContext.Session.Remove(CartKey);
        return Redirect("/cart");
    }
}
