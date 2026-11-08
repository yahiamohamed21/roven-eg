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

        // Discovers and applies all IEntityTypeConfiguration classes
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
