namespace Stockify.Models.Entities;

public class ProductFragranceNote
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int FragranceNoteId { get; set; }
    public FragranceNote FragranceNote { get; set; } = null!;

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
