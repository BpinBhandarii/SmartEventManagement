using System.ComponentModel.DataAnnotations;

namespace SmartEventManagement.ViewModels;

public class ForgotPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}
