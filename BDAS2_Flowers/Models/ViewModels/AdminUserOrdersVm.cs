namespace BDAS2_Flowers.Models.ViewModels
{
    public class AdminUserOrdersVm
    {
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public List<string> StatusNames { get; set; } = new();
        public List<AdminOrderRowVm> Orders { get; set; } = new();
    }

    public class AdminOrderRowVm
    {
        public string OrderNo { get; set; } = null!; 
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = null!;
        public string Delivery { get; set; } = null!;
        public string Shop { get; set; } = null!;
        public decimal Total { get; set; }
    }
}
