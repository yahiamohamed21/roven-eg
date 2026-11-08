using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stockify.Models.Entities;

namespace Stockify.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(o => o.OrderNumber)
            .IsUnique();

        builder.Property(o => o.TotalAmount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(o => o.ShippingAddress)
            .HasMaxLength(300);

        builder.Property(o => o.City)
            .HasMaxLength(100);

        builder.Property(o => o.CustomerName)
            .HasMaxLength(150);

        builder.Property(o => o.CustomerPhone)
            .HasMaxLength(30);

        builder.Property(o => o.CustomerEmail)
            .HasMaxLength(150);

        builder.Property(o => o.OrderNotes)
            .HasMaxLength(500);

        builder.Property(o => o.PaymentMethod)
            .HasMaxLength(50)
            .HasDefaultValue("Cash on Delivery");

        builder.HasOne(o => o.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.OrderItems)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

