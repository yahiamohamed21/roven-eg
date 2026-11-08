using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stockify.Models.Entities;

namespace Stockify.Data.Configurations;

public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Notes)
            .HasMaxLength(500);

        builder.HasOne(t => t.Product)
            .WithMany(p => p.InventoryTransactions)
            .HasForeignKey(t => t.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.Order)
            .WithMany(o => o.InventoryTransactions)
            .HasForeignKey(t => t.OrderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

