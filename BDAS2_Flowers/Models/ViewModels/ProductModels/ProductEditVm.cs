using System.ComponentModel.DataAnnotations;

namespace BDAS2_Flowers.Models.ViewModels.ProductModels
{
    public class ProductEditVm
    {
        public int? ProductId { get; set; }
        public int? MainPictureId { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = null!;

        [Range(0, 1_000_000)]
        public decimal Price { get; set; }

        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; }

        [Display(Name = "Type"), Range(1, int.MaxValue)]
        public int ProductTypeId { get; set; }

        public IEnumerable<(int Id, string Name)> Types { get; set; } = Enumerable.Empty<(int, string)>();
    }
}
