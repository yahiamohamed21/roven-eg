using System.ComponentModel.DataAnnotations;

namespace Stockify.Models.Entities;

public class PushSubscription
{
    public int Id { get; set; }

    [Required]
    [MaxLength(1024)]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string P256dh { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string Auth { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? UserId { get; set; }

    [MaxLength(512)]
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastSuccessAt { get; set; }
}

public class PushVapidSetting
{
    public int Id { get; set; }

    [Required]
    [MaxLength(256)]
    public string Subject { get; set; } = "https://roven-eg.online";

    [Required]
    [MaxLength(512)]
    public string PublicKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string PrivateKey { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
