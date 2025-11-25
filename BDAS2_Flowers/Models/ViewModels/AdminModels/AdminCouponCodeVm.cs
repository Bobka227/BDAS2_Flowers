namespace BDAS2_Flowers.Models.ViewModels.AdminModels
{
    public class AdminCouponCodeVm
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public decimal Bonus { get; set; }
        public DateTime DateExpiry { get; set; }
    }

    public class AdminCouponListVm
    {
        public string? Query { get; set; }
        public List<AdminCouponCodeVm> Rows { get; set; } = new();
    }
}