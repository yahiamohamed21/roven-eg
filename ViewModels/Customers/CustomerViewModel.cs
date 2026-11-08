using System.ComponentModel.DataAnnotations;

namespace Stockify.ViewModels.Customers;

public class CustomerViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Customer name is required.")]
    [StringLength(150, ErrorMessage = "Full name cannot exceed 150 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please provide a valid email address.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Please provide a valid phone number.")]
    [StringLength(30, ErrorMessage = "Phone number cannot exceed 30 characters.")]
    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }

    [StringLength(250, ErrorMessage = "Address cannot exceed 250 characters.")]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [Display(Name = "City / Region")]
    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string? City { get; set; }

    public int OrdersCount { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FullName)) return "CU";
            var parts = FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        }
    }
}
