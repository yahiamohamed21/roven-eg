using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stockify.Models.Entities;

namespace Stockify.Data.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.UnitPrice)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.HasOne(i => i.Product)
            .WithMany(p => p.OrderItems)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

