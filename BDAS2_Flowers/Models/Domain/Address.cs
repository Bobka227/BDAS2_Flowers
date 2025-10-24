namespace BDAS2_Flowers.Models.Domain;
public class Address
{
    public int PostalCode { get; set; }
    public string Street { get; set; } = null!;
    public int HouseNumber { get; set; }
    public int OrderId { get; set; }
}
