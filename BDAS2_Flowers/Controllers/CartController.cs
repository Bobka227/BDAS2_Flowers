// Controllers/CartController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using BDAS2_Flowers.Models.ViewModels;

[Authorize]
public class CartController : Controller
{
    private readonly IConfiguration _cfg;
    private const string CartKey = "CART";

    public CartController(IConfiguration cfg) => _cfg = cfg;

    [HttpPost("/cart/add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, int quantity = 1)
    {
        if (productId <= 0 || quantity <= 0) return Redirect(Request.Headers["Referer"].ToString());

        // достанем продукт (название + цена), чтобы отрисовать корзину без дополнительных запросов
        await using var con = new OracleConnection(_cfg.GetConnectionString("Oracle"));
        await con.OpenAsync();
        string title = "";
        decimal price = 0;
        await using (var cmd = new OracleCommand(
            @"SELECT Name, CAST(Price AS NUMBER(10,2)) FROM PRODUCT WHERE ProductId=:id", con))
        {
            cmd.Parameters.Add("id", productId);
            await using var rd = await cmd.ExecuteReaderAsync();
            if (await rd.ReadAsync())
            {
                title = rd.GetString(0);
                price = (decimal)rd.GetDecimal(1);
            }
            else
            {
                TempData["Error"] = "Produkt nebyl nalezen.";
                return Redirect(Request.Headers["Referer"].ToString());
            }
        }

        var cart = HttpContext.Session.GetJson<CartVm>(CartKey) ?? new CartVm();
        var line = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (line == null)
            cart.Items.Add(new CartItemVm { ProductId = productId, Title = title, Quantity = quantity, UnitPrice = price });
        else
            line.Quantity += quantity;

        HttpContext.Session.SetJson(CartKey, cart);
        TempData["Ok"] = "Přidáno do košíku.";
        return Redirect("/cart");
    }

    [HttpGet("/cart")]
    public IActionResult Index()
    {
        var cart = HttpContext.Session.GetJson<CartVm>(CartKey) ?? new CartVm();
        return View(cart);
    }

    [HttpPost("/cart/remove")]
    [ValidateAntiForgeryToken]
    public IActionResult Remove(int productId)
    {
        var cart = HttpContext.Session.GetJson<CartVm>(CartKey) ?? new CartVm();
        cart.Items.RemoveAll(i => i.ProductId == productId);
        HttpContext.Session.SetJson(CartKey, cart);
        return Redirect("/cart");
    }

    [HttpPost("/cart/clear")]
    [ValidateAntiForgeryToken]
    public IActionResult Clear()
    {
        HttpContext.Session.Remove(CartKey);
        return Redirect("/cart");
    }
}
