using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using BDAS2_Flowers.Models.ViewModels.OrderModels;

/// <summary>
/// View komponenta, která zobrazuje badge s počtem položek v košíku.
/// </summary>
public class CartBadgeViewComponent : ViewComponent
{
    /// <summary>
    /// Vrátí klíč session pro aktuální košík podle přihlášeného uživatele.
    /// </summary>
    /// <returns>
    /// Řetězec s klíčem košíku – pro přihlášeného uživatele ve tvaru <c>CART_USER_{UserId}</c>,
    /// pro anonymního uživatele <c>CART_ANON</c>.
    /// </returns>
    private string GetCartKey()
    {
        var userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
            return $"CART_USER_{userId}";

        return "CART_ANON";
    }

    /// <summary>
    /// Vykreslí view komponentu s počtem položek v košíku.
    /// </summary>
    /// <returns>
    /// Výsledek vykreslení view komponenty obsahující celkový počet kusů v košíku.
    /// </returns>
    public IViewComponentResult Invoke()
    {
        var cartKey = GetCartKey();

        var cart = HttpContext.Session.GetJson<CartVm>(cartKey) ?? new CartVm();
        var count = cart.Items.Sum(i => i.Quantity);

        return View(count);
    }
}
