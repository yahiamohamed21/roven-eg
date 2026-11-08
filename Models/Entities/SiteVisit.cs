using System;

namespace Stockify.Models.Entities;

public class SiteVisit
{
    public int Id { get; set; }
    public string? SessionId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string Path { get; set; } = string.Empty;
    public string? Referer { get; set; }
    public string? DeviceType { get; set; } // "Mobile", "Desktop", "Tablet"
    public DateTime VisitedAt { get; set; } = DateTime.UtcNow;
}

