using BDAS2_Flowers.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

public class CartBadgeViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        var cart = HttpContext.Session.GetJson<CartVm>("CART") ?? new CartVm();
        var count = cart.Items.Sum(i => i.Quantity);
        return View(count);
    }
}
