namespace BDAS2_Flowers.Models.ViewModels.UserProfileModels
{
    public class ChangePasswordVm
    {
        public string CurrentPassword { get; set; } = "";
        public string NewPassword { get; set; } = "";
        public string ConfirmNewPassword { get; set; } = "";
    }
}
