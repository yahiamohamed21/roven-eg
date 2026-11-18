using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stockify.Models.Entities;

public class HomeSetting
{
    [Key]
    public int Id { get; set; } = 1;

    // Announcement Top Bar
    [MaxLength(300)]
    public string AnnouncementText { get; set; } = "Maison ROVEN • Complimentary Nationwide Delivery Across Egypt";
    public bool AnnouncementActive { get; set; } = true;

    // Default Fragrance Tagline for Catalog & Products
    [MaxLength(200)]
    public string DefaultFragranceTagline { get; set; } = "Extrait de Parfum • 30% Pure Oil";

    // Hero Section (Slide 1)
    [MaxLength(300)]
    public string HeroTitle { get; set; } = "L'Art de l'Extrait • 30% Pure Oil Concentration";
    public string HeroSubtitle { get; set; } = "Experience the eternal sillage of Grasse botanicals, rare Assam oud, and cold-matured flacons.";
    [MaxLength(150)]
    public string HeroBadgeText { get; set; } = "MAISON ROVEN • PARIS • CAIRO";

    // Hero Section (Slide 2 - Private Reserve)
    [MaxLength(300)]
    public string? Banner2Title { get; set; } = "Aged Assam Oud & Golden Resins.";
    public string? Banner2Subtitle { get; set; } = "An opulent symphony of aged Assam agarwood, golden amber crystals, frankincense tears, and saffron stigma.";
    [MaxLength(150)]
    public string? Banner2BadgeText { get; set; } = "MAISON ROVEN • PRIVATE RESERVE";
    [MaxLength(100)]
    public string? Banner2ButtonText { get; set; } = "Shop Oud & Oriental";
    [MaxLength(200)]
    public string? Banner2ButtonLink { get; set; } = "/Store?categoryId=3";
    public bool Banner2Active { get; set; } = true;

    // Hero Section (Slide 3 - Discovery Coffret)
    [MaxLength(300)]
    public string? Banner3Title { get; set; } = "Discover Your Signature Scent in One Coffret.";
    public string? Banner3Subtitle { get; set; } = "Five bespoke 10ml travel sprays presented in the signature ROVEN burgundy presentation box. Includes voucher.";
    [MaxLength(150)]
    public string? Banner3BadgeText { get; set; } = "MAISON ROVEN • THE DISCOVERY COFFRET";
    [MaxLength(100)]
    public string? Banner3ButtonText { get; set; } = "Claim Discovery Set";
    [MaxLength(200)]
    public string? Banner3ButtonLink { get; set; } = "/Store?categoryId=4";
    public bool Banner3Active { get; set; } = true;

    // Spotlight / Deal of the Day
    public int? SpotlightProductId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SpotlightPrice { get; set; } = 2850.00m;
    public DateTime? SpotlightEndTime { get; set; }
    public int SpotlightAllocatedPercent { get; set; } = 82;
    public int SpotlightRemainingCount { get; set; } = 51;
    public bool SpotlightActive { get; set; } = true;

    // Spotlight / Special Offers Section Customization
    [MaxLength(150)]
    public string SpotlightBadgeText { get; set; } = "EXCLUSIVE ATELIER OFFERS & DEALS";

    [MaxLength(300)]
    public string SpotlightTitle { get; set; } = "عروض وتخفيضات دار روفين الحصرية";

    [MaxLength(600)]
    public string SpotlightSubtitle { get; set; } = "إصدارات عطرية مختارة بتخفيضات استثنائية لفترة محدودة، مع شحن لكافة المحافظات وإمكانية معاينة الطرد قبل الدفع عند الاستلام.";

    // Infinite Moving Marquee Announcement Tape (Under Hero Banners)
    [MaxLength(500)]
    public string MarqueeText { get; set; } = "OFFER 20% OFF ON ALL LUXURY EXTRAITS • كود الخصم: ROVEN20 • عطور أصلية بتركيز 30% زيوت نقية • شحن مجاني ودفع عند الاستلام مع المعاينة في كافة محافظات مصر";

    [MaxLength(100)]
    public string MarqueeBadgeText { get; set; } = "OFFER 20%";

    public bool MarqueeActive { get; set; } = true;

    public int MarqueeSpeedSeconds { get; set; } = 25;

    // Social Links
    [MaxLength(300)]
    public string FacebookUrl { get; set; } = "https://facebook.com/roven.parfumerie";
    [MaxLength(300)]
    public string InstagramUrl { get; set; } = "https://instagram.com/roven.parfums";
    [MaxLength(100)]
    public string WhatsAppNumber { get; set; } = "+201006856199";

    [NotMapped]
    public string NormalizedWhatsAppNumber => NormalizeWhatsAppNumber(WhatsAppNumber);

    public static string NormalizeWhatsAppNumber(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "201006856199";
        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(digits)) return "201006856199";
        if (digits.StartsWith("00")) digits = digits.Substring(2);
        if (digits.Length == 11 && digits.StartsWith("01")) digits = "2" + digits;
        else if (digits.Length == 10 && (digits.StartsWith("10") || digits.StartsWith("11") || digits.StartsWith("12") || digits.StartsWith("15"))) digits = "20" + digits;
        return digits;
    }
    [MaxLength(100)]
    public string ContactPhone { get; set; } = "+20 (02) 19888";
    [MaxLength(150)]
    public string ContactEmail { get; set; } = "concierge@roven.local";
    [MaxLength(200)]
    public string ContactWorkingHours { get; set; } = "Daily: 10:00 AM – 11:00 PM CLT";
    [MaxLength(200)]
    public string AtelierName { get; set; } = "Zamalek & New Cairo Salons";
    [MaxLength(200)]
    public string AtelierAddress { get; set; } = "Cairo & Giza, Arab Republic of Egypt";

    // Best Sellers Section Customization
    [MaxLength(150)]
    public string BestSellersBadgeText { get; set; } = "Most Requested";

    [MaxLength(300)]
    public string BestSellersTitle { get; set; } = "Top Acclaimed ROVEN Parfums";

    public string BestSellersSubtitle { get; set; } = "The fragrances most cherished by our clientele across Egypt this month";

    [MaxLength(500)]
    public string? BestSellersProductIds { get; set; } = null;

    // Featured Section Customization
    [MaxLength(150)]
    public string FeaturedBadgeText { get; set; } = "Master Distillations";

    [MaxLength(300)]
    public string FeaturedTitle { get; set; } = "Featured ROVEN Parfums";

    public string FeaturedSubtitle { get; set; } = "Our most acclaimed fragrant creations formulated with precious Grasse and oriental oils";

    [MaxLength(150)]
    public string FeaturedExploreText { get; set; } = "Explore Full Collection";

    [MaxLength(500)]
    public string? FeaturedProductIds { get; set; } = null;

    // Section Toggles
    public bool ShowBrandHero { get; set; } = true;
    public bool ShowHeroBanners { get; set; } = true;
    public bool ShowTrustBadges { get; set; } = true;
    public bool ShowCategoriesShowcase { get; set; } = true;
    public bool ShowFeaturedSection { get; set; } = true;
    public bool ShowBestSellersSection { get; set; } = true;
    public bool ShowTestimonialsSection { get; set; } = true;
    public bool ShowFaqSection { get; set; } = true;
    public bool ShowNewsletterSection { get; set; } = true;

    // Homepage Section Display Orders (1 through 11)
    public int OrderBrandHero { get; set; } = 1;
    public int OrderMarquee { get; set; } = 2;
    public int OrderTrustBadges { get; set; } = 3;
    public int OrderHeroBanners { get; set; } = 4;
    public int OrderCategories { get; set; } = 5;
    public int OrderOffers { get; set; } = 6;
    public int OrderFeatured { get; set; } = 7;
    public int OrderBestSellers { get; set; } = 8;
    public int OrderTestimonials { get; set; } = 9;
    public int OrderFaq { get; set; } = 10;
    public int OrderNewsletter { get; set; } = 11;
}
