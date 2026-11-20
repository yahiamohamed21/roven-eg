using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Stockify.Models.Entities;

namespace Stockify.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<HomeSetting> HomeSettings => Set<HomeSetting>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<SiteVisit> SiteVisits => Set<SiteVisit>();
    public DbSet<AbandonedCheckout> AbandonedCheckouts => Set<AbandonedCheckout>();
    public DbSet<Governorate> Governorates => Set<Governorate>();
    public DbSet<HomeOffer> HomeOffers => Set<HomeOffer>();
    public DbSet<HeroBanner> HeroBanners => Set<HeroBanner>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<MediaDeletionJob> MediaDeletionJobs => Set<MediaDeletionJob>();
    public DbSet<HomeFaqItem> HomeFaqItems => Set<HomeFaqItem>();

    // Fragrance Discovery (Find Your Scent)
    public DbSet<FragranceNote> FragranceNotes => Set<FragranceNote>();
    public DbSet<FragranceNoteQuickPick> FragranceNoteQuickPicks => Set<FragranceNoteQuickPick>();
    public DbSet<ProductTargetAudience> ProductTargetAudiences => Set<ProductTargetAudience>();
    public DbSet<ProductSeason> ProductSeasons => Set<ProductSeason>();
    public DbSet<ProductFragranceNote> ProductFragranceNotes => Set<ProductFragranceNote>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ProductReview>(b =>
        {
            b.HasOne(r => r.Product)
             .WithMany(p => p.Reviews)
             .HasForeignKey(r => r.ProductId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(r => r.ProductId);
            b.HasIndex(r => r.IsApproved);
            b.HasIndex(r => r.ShowOnHome);
            b.HasIndex(r => r.CreatedAt);
        });

        builder.Entity<HeroBanner>(b =>
        {
            b.HasIndex(x => x.DisplayOrder);
            b.HasIndex(x => x.IsActive);
        });

        builder.Entity<HomeOffer>(b =>
        {
            b.HasOne(o => o.Product)
             .WithMany()
             .HasForeignKey(o => o.ProductId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(o => o.SecondProduct)
             .WithMany()
             .HasForeignKey(o => o.SecondProductId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(o => o.IsActive);
            b.HasIndex(o => o.DisplayOrder);
        });

        builder.Entity<AbandonedCheckout>(b =>
        {
            b.Property(a => a.TotalAmount).HasPrecision(18, 2);
            b.HasOne(a => a.RecoveredOrder)
             .WithMany()
             .HasForeignKey(a => a.RecoveredOrderId)
             .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(a => a.IsRecovered);
            b.HasIndex(a => a.CustomerPhone);
        });

        builder.Entity<SiteVisit>(b =>
        {
            b.HasIndex(v => v.VisitedAt);
            b.HasIndex(v => v.SessionId);
        });

        // Fragrance Discovery (Find Your Scent)
        builder.Entity<FragranceNote>(b =>
        {
            b.HasIndex(n => n.Name).IsUnique();
            b.HasIndex(n => n.Category);
            b.HasIndex(n => n.IsActive);
            b.HasIndex(n => n.DisplayOrder);
        });

        builder.Entity<FragranceNoteQuickPick>(b =>
        {
            b.HasKey(qp => new { qp.FragranceNoteId, qp.Context });
            b.HasOne(qp => qp.FragranceNote)
             .WithMany(fn => fn.QuickPicks)
             .HasForeignKey(qp => qp.FragranceNoteId)
             .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(qp => qp.Context);
            b.HasIndex(qp => qp.DisplayOrder);
        });

        builder.Entity<ProductTargetAudience>(b =>
        {
            b.HasKey(pta => new { pta.ProductId, pta.Audience });
            b.HasOne(pta => pta.Product)
             .WithMany(p => p.TargetAudiences)
             .HasForeignKey(pta => pta.ProductId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProductSeason>(b =>
        {
            b.HasKey(ps => new { ps.ProductId, ps.Season });
            b.HasOne(ps => ps.Product)
             .WithMany(p => p.Seasons)
             .HasForeignKey(ps => ps.ProductId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProductFragranceNote>(b =>
        {
            b.HasKey(pfn => new { pfn.ProductId, pfn.FragranceNoteId });
            b.HasOne(pfn => pfn.Product)
             .WithMany(p => p.ProductFragranceNotes)
             .HasForeignKey(pfn => pfn.ProductId)
             .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(pfn => pfn.FragranceNote)
             .WithMany(fn => fn.ProductFragranceNotes)
             .HasForeignKey(pfn => pfn.FragranceNoteId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Product>(b =>
        {
            b.HasIndex(p => p.IsFragranceProfileApproved);
        });

        // Discovers and applies all IEntityTypeConfiguration classes
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
