using System;

namespace BDAS2_Flowers.Models.Domain;
public class Cupon
{
    public int PaymentId { get; set; }
    public DateTime DateOfExpiry { get; set; }
    public decimal Bonus { get; set; }
}
