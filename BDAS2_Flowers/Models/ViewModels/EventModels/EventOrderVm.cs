using System.ComponentModel.DataAnnotations;

namespace BDAS2_Flowers.Models.ViewModels.EventModels
{
    public class EventOrderVm
    {
        public int UserId { get; set; }
        public int EventTypeId { get; set; }
        public DateTime EventDate { get; set; }

        public int ShopId { get; set; }
        public List<(int Id, string Name)> Shops { get; set; } = new();

        public int? AddressId { get; set; } 
        public string? Street { get; set; }
        public int? HouseNumber { get; set; }
        public int? PostalCode { get; set; }

        public int? Quantity { get; set; } = 1;

        public string PaymentType { get; set; } = "card";
    }



}
