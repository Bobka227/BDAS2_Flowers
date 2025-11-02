namespace BDAS2_Flowers.Models.ViewModels
{
    public class ProfileVm
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
    }
}
