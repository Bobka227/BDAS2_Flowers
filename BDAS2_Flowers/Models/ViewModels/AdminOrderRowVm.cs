namespace BDAS2_Flowers.Models.ViewModels
{
    public class AdminOrderRowVm
    {
        public string OrderNo { get; set; } = null!;
        public DateTime OrderDate { get; set; }
        public string Customer { get; set; } = "";
        public string Status { get; set; } = "";
        public string Delivery { get; set; } = "";
        public string Shop { get; set; } = "";
        public decimal? Total { get; set; }
    }
}
