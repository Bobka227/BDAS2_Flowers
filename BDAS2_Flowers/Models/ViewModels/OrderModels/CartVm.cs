namespace BDAS2_Flowers.Models.ViewModels.OrderModels
{
    public class CartItemVm
    {
        public int ProductId { get; set; }
        public string Title { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => UnitPrice * Quantity;
    }

    public class CartVm
    {
        public List<CartItemVm> Items { get; set; } = new();
        public decimal Total => Items.Sum(i => i.UnitPrice * i.Quantity);
    }
}
