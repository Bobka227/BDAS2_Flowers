using System;

namespace BDAS2_Flowers.Models.Domain;
public class Payment
{
    public int PaymentId { get; set; }
    public int Amount { get; set; }
    public DateTime Date { get; set; }
    public string PaymentType { get; set; } = null!;
}