using System;

namespace BDAS2_Flowers.Models.Domain;
public class Order
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public int DeliveryMethodId { get; set; }
    public int PaymentId { get; set; }
    public int StatusId { get; set; }
    public int UserId { get; set; }
    public int ShopId { get; set; }
}