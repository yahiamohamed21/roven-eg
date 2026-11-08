using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stockify.Models.Entities;

namespace Stockify.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.FullName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.Email)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.PhoneNumber)
            .HasMaxLength(30);

        builder.Property(c => c.Address)
            .HasMaxLength(250);
    }
}

