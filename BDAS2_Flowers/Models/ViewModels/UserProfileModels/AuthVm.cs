using System.ComponentModel.DataAnnotations;

namespace BDAS2_Flowers.Models.ViewModels.UserProfileModels;

public class RegisterVm
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, MinLength(6)] public string Password { get; set; } = "";
    [Required] public string FirstName { get; set; } = "";
    [Required] public string LastName { get; set; } = "";
    [Required, RegularExpression(@"^\d{7,15}$", ErrorMessage = "Jen čísla")] public string Phone { get; set; } = "";
}

public class LoginVm
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
