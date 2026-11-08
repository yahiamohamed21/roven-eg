using System.ComponentModel.DataAnnotations;

namespace Stockify.ViewModels.Account;

public class ProfileViewModel
{
    [Required]
    [Display(Name = "Full Name")]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }

    public string Role { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string? CurrentPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    [MinLength(6)]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirm New Password")]
    [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
    public string? ConfirmPassword { get; set; }
}

