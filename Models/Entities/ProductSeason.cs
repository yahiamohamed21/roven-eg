using Stockify.Models.Enums;

namespace Stockify.Models.Entities;

public class ProductSeason
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public FragranceSeason Season { get; set; }
}
