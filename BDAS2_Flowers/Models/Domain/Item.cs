namespace BDAS2_Flowers.Models.Domain;
public class Item
{
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
}
