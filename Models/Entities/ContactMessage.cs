using System;
using System.ComponentModel.DataAnnotations;

namespace Stockify.Models.Entities;

public class ContactMessage
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? Email { get; set; }

    [Required]
    [MaxLength(100)]
    public string Subject { get; set; } = "General Inquiry";

    [Required]
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; } = false;

    public string? AdminNotes { get; set; }
}

