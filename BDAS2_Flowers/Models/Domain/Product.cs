namespace BDAS2_Flowers.Models.Domain;
public class Product
{
    public int ProductId { get; set; }
    public string Name { get; set; } = null!;
    public int Price { get; set; }
    public int StockQuantity { get; set; }
    public int ProductTypeId { get; set; }
    public int? PictureId { get; set; }
}
