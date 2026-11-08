using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stockify.Models.Entities;

public class ProductReview
{
    [Key]
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Required]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? CustomerEmail { get; set; }

    [MaxLength(50)]
    public string? CustomerPhone { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; } = 5;

    [Required]
    [MaxLength(1000)]
    public string Comment { get; set; } = string.Empty;

    public bool IsApproved { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

