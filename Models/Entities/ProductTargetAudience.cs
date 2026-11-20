using Stockify.Models.Enums;

namespace Stockify.Models.Entities;

public class ProductTargetAudience
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public AudienceTarget Audience { get; set; }
}
