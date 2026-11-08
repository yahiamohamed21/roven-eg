using System.ComponentModel.DataAnnotations;
using Stockify.Models.Entities;
using Stockify.Models.Enums;

namespace Stockify.ViewModels.Store;

public class StoreHomeViewModel
{
    public List<StoreProductCardViewModel> FeaturedProducts { get; set; } = new();
    public List<StoreProductCardViewModel> LatestProducts { get; set; } = new();
    public List<StoreProductCardViewModel> BestSellers { get; set; } = new();
    public StoreProductCardViewModel? DealOfTheDay { get; set; }
    public List<HomeOfferItemViewModel> SpecialOffers { get; set; } = new();
    public List<HeroBanner> HeroBanners { get; set; } = new();
    public List<StoreCategoryViewModel> Categories { get; set; } = new();
    public int TotalCatalogCount { get; set; }
    public int TotalCustomersCount { get; set; }
    public int TotalOrdersCompleted { get; set; }
    public HomeSetting? Settings { get; set; }
}

public class HomeOfferItemViewModel
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public decimal FirstProductPrice { get; set; }

    // Second Product Support for Duo / 2 Perfumes Bundle Offers
    public int? SecondProductId { get; set; }
    public string? SecondProductName { get; set; }
    public string? SecondProductSku { get; set; }
    public string? SecondProductCategoryName { get; set; }
    public string? SecondProductImageUrl { get; set; }
    public decimal? SecondProductPrice { get; set; }

    public string? Title { get; set; }
    public bool IsDuoOffer => SecondProductId.HasValue && SecondProductId.Value > 0 && !string.IsNullOrWhiteSpace(SecondProductName);
    public string DisplayTitle => !string.IsNullOrWhiteSpace(Title) ? Title : (IsDuoOffer ? $"{ProductName} + {SecondProductName}" : ProductName);

    public decimal OriginalPrice => FirstProductPrice + (SecondProductPrice ?? 0m);
    public decimal OfferPrice { get; set; }
    public int DiscountPercent => (OriginalPrice > 0 && OfferPrice < OriginalPrice) ? (int)Math.Round(((OriginalPrice - OfferPrice) / OriginalPrice) * 100m) : 0;
    public string? BadgeText { get; set; }
    public DateTime? EndTime { get; set; }
    public int AllocatedPercent { get; set; } = 82;
    public int RemainingCount { get; set; } = 15;
    public bool IsActive { get; set; } = true;
    public bool IsOutOfStock { get; set; }
}

public class StoreCategoryViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ProductsCount { get; set; }
}

public class StoreProductCardViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string? ImageUrl { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }
    public bool IsOutOfStock => StockQuantity <= 0;
    public bool IsLowStock => StockQuantity > 0 && StockQuantity <= 5;
}

public class StoreCatalogViewModel
{
    public List<StoreProductCardViewModel> Products { get; set; } = new();
    public List<StoreCategoryViewModel> Categories { get; set; } = new();
    public int? SelectedCategoryId { get; set; }
    public string? SelectedCategoryName { get; set; }
    public string? SearchTerm { get; set; }
    public string? SearchCorrectionNotice { get; set; }
    public string CurrentSort { get; set; } = "newest";
    public bool InStockOnly { get; set; }
    public int TotalCount { get; set; }
}

public class StoreProductDetailsViewModel
{
    public StoreProductCardViewModel Product { get; set; } = new();
    public List<StoreProductCardViewModel> RelatedProducts { get; set; } = new();
    public List<ProductReviewViewModel> Reviews { get; set; } = new();
    public double AverageRating => Reviews.Any() ? Math.Round(Reviews.Average(r => r.Rating), 1) : 5.0;
    public int ReviewsCount => Reviews.Count;
}

public class ProductReviewViewModel
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int Rating { get; set; } = 5;
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CartItemViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public int StockAvailable { get; set; }
    public string? ImageUrl { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public class CartViewModel
{
    public List<CartItemViewModel> Items { get; set; } = new();
    public decimal Subtotal => Items.Sum(i => i.LineTotal);
    public string? AppliedCouponCode { get; set; }
    public decimal DiscountAmount { get; set; } = 0m;
    public decimal Tax => 0m; // Zero VAT policy across store
    public decimal ShippingFee { get; set; } = 0m;
    public decimal GrandTotal => Math.Max(0m, Subtotal - DiscountAmount + ShippingFee);
    public int TotalItemCount => Items.Sum(i => i.Quantity);
}

public class CheckoutViewModel
{
    public string? CouponCode { get; set; }
    public decimal DiscountAmount { get; set; } = 0m;
    public decimal ShippingFee { get; set; } = 0m;
    public List<Stockify.Models.Entities.Governorate> AvailableGovernorates { get; set; } = new();

    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(150, ErrorMessage = "Name cannot exceed 150 characters.")]
    [Display(Name = "Full Name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile phone number is required.")]
    [Phone(ErrorMessage = "Please provide a valid Egyptian phone number.")]
    [StringLength(30, ErrorMessage = "Phone number cannot exceed 30 characters.")]
    [Display(Name = "Phone Number")]
    public string CustomerPhone { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Please provide a valid email address.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    [Display(Name = "Email Address (Optional)")]
    public string? CustomerEmail { get; set; }

    [Required(ErrorMessage = "Shipping address is required.")]
    [StringLength(300, ErrorMessage = "Address cannot exceed 300 characters.")]
    [Display(Name = "Street Address, Building & Flat #")]
    public string ShippingAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please specify your City/Governorate.")]
    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    [Display(Name = "City / Governorate")]
    public string City { get; set; } = "Cairo";

    [StringLength(500, ErrorMessage = "Order notes cannot exceed 500 characters.")]
    [Display(Name = "Delivery Instructions / Notes (Optional)")]
    public string? OrderNotes { get; set; }

    public string PaymentMethod { get; set; } = "Cash on Delivery";

    public CartViewModel Cart { get; set; } = new();
}

public class OrderTrackingViewModel
{
    public string? QueryOrderNumber { get; set; }
    public string? QueryPhone { get; set; }
    public Order? Order { get; set; }
    public bool HasSearched { get; set; }
    public bool NotFound => HasSearched && Order == null;
}

