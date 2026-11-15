using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stockify.Models.Entities;

namespace Stockify.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for MediaDeletionJob entity.
/// Defines table mapping, property constraints, and performance indexes.
/// </summary>
public class MediaDeletionJobConfiguration : IEntityTypeConfiguration<MediaDeletionJob>
{
    public void Configure(EntityTypeBuilder<MediaDeletionJob> builder)
    {
        builder.ToTable("MediaDeletionJobs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ImageUrl)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.PublicId)
            .HasMaxLength(255);

        builder.Property(x => x.LockedBy)
            .HasMaxLength(100);

        builder.Property(x => x.LastError)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        // Performance index for retrieving due / pending jobs
        builder.HasIndex(x => new { x.Status, x.NextRetryUtc })
            .HasDatabaseName("IX_MediaDeletionJobs_Status_NextRetry");

        // Index for fast duplicate checks by ImageUrl
        builder.HasIndex(x => x.ImageUrl)
            .HasDatabaseName("IX_MediaDeletionJobs_ImageUrl");
    }
}
