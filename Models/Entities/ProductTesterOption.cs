namespace Stockify.Models.Entities;

public class ProductTesterOption
{
    public int TesterProductId { get; set; }
    public Product? TesterProduct { get; set; }

    public int FragranceProductId { get; set; }
    public Product? FragranceProduct { get; set; }

    public int DisplayOrder { get; set; } = 0;
}
