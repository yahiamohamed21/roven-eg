using Microsoft.EntityFrameworkCore;
using Stockify.ViewModels.Store;

namespace Stockify.Data;

public static class StoreProductSizeHelper
{
    /// <summary>
    /// Batch-loads active bottle sizes and synced customer selling prices for storefront product cards.
    /// Wholesale CostPrice and ProfitAmount are intentionally excluded.
    /// </summary>
    public static async Task HydrateSizesAsync(this ApplicationDbContext context, IEnumerable<StoreProductCardViewModel?> cards)
    {
        var cardList = cards.Where(c => c != null).Select(c => c!).ToList();
        if (cardList.Count == 0) return;

        var productIds = cardList.Select(c => c.Id).Distinct().ToList();
        var sizesByProduct = await context.ProductSizes
            .AsNoTracking()
            .Where(s => productIds.Contains(s.ProductId))
            .OrderByDescending(s => s.IsDefault)
            .ThenBy(s => s.DisplayOrder)
            .Select(s => new
            {
                s.ProductId,
                ViewModel = new StoreProductSizeViewModel
                {
                    Id = s.Id,
                    SizeLabel = s.SizeLabel,
                    SellingPrice = s.SellingPrice,
                    IsDefault = s.IsDefault
                }
            })
            .ToListAsync();

        var testerMetaByProduct = await context.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.IsTester)
            .Select(p => new
            {
                p.Id,
                p.TesterRequiredCount,
                Options = p.TesterOptions
                    .Where(o => o.FragranceProduct != null && o.FragranceProduct.IsActive)
                    .OrderBy(o => o.DisplayOrder)
                    .ThenBy(o => o.FragranceProduct!.Name)
                    .Select(o => new StoreTesterOptionViewModel
                    {
                        FragranceProductId = o.FragranceProductId,
                        Name = o.FragranceProduct!.Name,
                        Sku = o.FragranceProduct.Sku ?? "",
                        ImageUrl = o.FragranceProduct.ImageUrl
                    })
                    .ToList()
            })
            .ToDictionaryAsync(x => x.Id);

        var lookup = sizesByProduct
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ViewModel).ToList());

        foreach (var card in cardList)
        {
            if (lookup.TryGetValue(card.Id, out var sizes) && sizes.Count > 0)
            {
                card.Sizes = sizes;
                var defSize = sizes.FirstOrDefault(s => s.IsDefault) ?? sizes[0];
                card.Price = defSize.SellingPrice;
            }
            else if (card.Sizes.Count == 0)
            {
                card.Sizes = new List<StoreProductSizeViewModel>();
            }

            if (testerMetaByProduct.TryGetValue(card.Id, out var testerMeta))
            {
                card.IsTester = true;
                card.TesterRequiredCount = testerMeta.TesterRequiredCount > 0 ? testerMeta.TesterRequiredCount : 4;
                card.AvailableTesterFragrances = testerMeta.Options;
            }
            else
            {
                card.IsTester = false;
                card.AvailableTesterFragrances = new List<StoreTesterOptionViewModel>();
            }
        }
    }
}
