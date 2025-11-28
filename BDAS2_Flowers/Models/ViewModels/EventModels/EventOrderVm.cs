using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BDAS2_Flowers.Models.ViewModels.EventModels
{
    public class EventProductChoiceVm
    {
        public int ProductId { get; set; }
        public string Title { get; set; } = "";
        public string? Subtitle { get; set; }
        public decimal PriceFrom { get; set; }
        public bool Recommended { get; set; }
        public int Quantity { get; set; }
    }

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

        [Display(Name = "Kód kuponu")]
        public string? CouponCode { get; set; }

        [Display(Name = "Číslo karty")]
        public string? CardNumber { get; set; }

        public List<EventProductChoiceVm> Products { get; set; } = new();
    }
}
