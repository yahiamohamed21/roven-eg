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
    public string WhatsAppNumber { get; set; } = "+201008889999";
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

    // Section Toggles
    public bool ShowTrustBadges { get; set; } = true;
    public bool ShowCategoriesShowcase { get; set; } = true;
    public bool ShowFeaturedSection { get; set; } = true;
    public bool ShowBestSellersSection { get; set; } = true;
    public bool ShowFaqSection { get; set; } = true;
    public bool ShowNewsletterSection { get; set; } = true;
}
