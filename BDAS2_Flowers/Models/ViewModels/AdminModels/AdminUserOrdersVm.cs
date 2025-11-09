namespace BDAS2_Flowers.Models.ViewModels.AdminModels
{
    public class AdminUserOrdersVm
    {
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public List<string> StatusNames { get; set; } = new();
        public List<AdminOrderRowVm> Orders { get; set; } = new();
    }
}
