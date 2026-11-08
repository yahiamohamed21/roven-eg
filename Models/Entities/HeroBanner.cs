using System;
using System.ComponentModel.DataAnnotations;

namespace Stockify.Models.Entities;

public class HeroBanner
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = "Hero Banner";

    [Required]
    [MaxLength(500)]
    public string DesktopImageUrl { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? MobileImageUrl { get; set; }

    [MaxLength(500)]
    public string? RedirectLink { get; set; } = "/Store";

    [MaxLength(250)]
    public string? AltText { get; set; }

    public int DisplayOrder { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

