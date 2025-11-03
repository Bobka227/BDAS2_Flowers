namespace BDAS2_Flowers.Models.ViewModels
{
    public class ProfileOrderRowVm
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = "";
        public string Delivery { get; set; } = "";
        public string Shop { get; set; } = "";
        public decimal Total { get; set; }
    }

    public class ProfileVm
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";

        public List<ProfileOrderRowVm> Orders { get; set; } = new();
    }
}
