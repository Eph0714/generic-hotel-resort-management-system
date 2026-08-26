using System.ComponentModel.DataAnnotations;

namespace HotelResortMS.Web.Models.Account;

public class LoginViewModel
{
    // Bound/validated as an email address (FindByEmailAsync in AccountController) - the
    // login screen just labels the field "Username" per the UI spec, since that's the
    // term staff are used to even though the underlying value is their email.
    [Required(ErrorMessage = "Username is required."), EmailAddress(ErrorMessage = "Enter a valid email address."), Display(Name = "Username")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
