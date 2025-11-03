namespace BDAS2_Flowers.Models.ViewModels
{
    public class IdNameVm { public int Id { get; set; } public string Name { get; set; } }

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

        // адрес
        public bool UseNewAddress { get; set; } = true;
        public int? AddressId { get; set; }      // когда UseNewAddress = false (выбор из списка)
        public int PostalCode { get; set; }      // когда UseNewAddress = true (ручной ввод)
        public string Street { get; set; } = "";
        public int HouseNumber { get; set; }

        public string PaymentType { get; set; } = "cash"; // 'cash'|'card'|'cupon'
        public List<OrderItemVm> Items { get; set; } = new();

        // выпадающие списки
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

    public class OrderDetailsVm
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public string Customer { get; set; } = "";
        public string Status { get; set; } = "";
        public string Delivery { get; set; } = "";
        public string Shop { get; set; } = "";
        public decimal Total { get; set; }
        public List<OrderItemDetailsVm> Items { get; set; } = new();
    }
}
