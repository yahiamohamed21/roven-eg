using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stockify.Models.Entities;

namespace Stockify.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(p => p.Sku)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(p => p.Sku)
            .IsUnique();

        builder.Property(p => p.Price)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.ImageUrl)
            .HasMaxLength(300);

        builder.Property(p => p.IsActive)
            .HasDefaultValue(true);

        builder.Property(p => p.IsPublished)
            .HasDefaultValue(true);

        builder.Property(p => p.IsFeatured)
            .HasDefaultValue(false);
    }
}

