using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using BDAS2_Flowers.Models.ViewModels.OrderModels;

public class CartBadgeViewComponent : ViewComponent
{
    private string GetCartKey()
    {
        var userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
            return $"CART_USER_{userId}";

        return "CART_ANON";
    }

    public IViewComponentResult Invoke()
    {
        var cartKey = GetCartKey();

        var cart = HttpContext.Session.GetJson<CartVm>(cartKey) ?? new CartVm();
        var count = cart.Items.Sum(i => i.Quantity);

        return View(count);
    }
}