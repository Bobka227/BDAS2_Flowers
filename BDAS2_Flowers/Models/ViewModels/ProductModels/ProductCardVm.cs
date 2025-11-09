namespace BDAS2_Flowers.Models.ViewModels.ProductModels
{
    public class ProductCardVm
    {
        public int ProductId { get; set; }
        public string Title { get; set; } = null!;
        public string? Subtitle { get; set; }
        public decimal PriceFrom { get; set; }
        public string ImageUrl { get; set; } = "/img/placeholder.jpg";
    }
}
