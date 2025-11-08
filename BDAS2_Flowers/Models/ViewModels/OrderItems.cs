namespace BDAS2_Flowers.Models.ViewModels
{
    public class IdNameVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class OrderItemVm
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class OrderCreateVm
    {
        public int UserId { get; set; }
        public int DeliveryMethodId { get; set; }
        public int ShopId { get; set; }

        public bool UseNewAddress { get; set; } = true;
        public int? AddressId { get; set; }
        public int PostalCode { get; set; }
        public string Street { get; set; } = "";
        public int HouseNumber { get; set; }
        public string PaymentType { get; set; } = "cash";
        public List<OrderItemVm> Items { get; set; } = new();
        public List<IdNameVm> DeliveryMethods { get; set; } = new();
        public List<IdNameVm> Shops { get; set; } = new();
        public List<IdNameVm> Addresses { get; set; } = new();
        public List<IdNameVm> Products { get; set; } = new();
    }

    public class OrderItemDetailsVm
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class AdminOrdersListVm
    {
        public string? Status { get; set; }
        public string? Query { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public long Total { get; set; }
        public int TotalPages => (int)Math.Max(1, (Total + PageSize - 1) / PageSize);
        public List<string> StatusNames { get; set; } = new();
        public List<AdminOrderRowVm> Rows { get; set; } = new();
    }
    
    public class OrderDetailsVm
    {
        public int OrderId { get; set; }
        public string PublicNo { get; set; } = "";
        public DateTime OrderDate { get; set; }
        public string Customer { get; set; } = "";
        public string Status { get; set; } = "";
        public string Delivery { get; set; } = "";
        public string Shop { get; set; } = "";
        public decimal Total { get; set; }
        public List<OrderItemDetailsVm> Items { get; set; } = new();
        public List<string> StatusNames { get; set; } = new();
    }
}
