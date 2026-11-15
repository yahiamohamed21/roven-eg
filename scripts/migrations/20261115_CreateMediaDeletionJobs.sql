-- ==============================================================================
-- DATABASE MIGRATION SCRIPT: Create MediaDeletionJobs Outbox Table
-- Target Environment: SQL Server 2019+ / Azure SQL Database
-- Description: Creates persistent outbox table for reliable media deletions,
--              ensuring pending deletions survive server restarts and crashes.
-- Status: PENDING REPOSITORY OWNER / DATABASE ADMINISTRATOR APPROVAL
-- ==============================================================================

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'MediaDeletionJobs')
BEGIN
    PRINT 'Creating table [dbo].[MediaDeletionJobs]...';

    CREATE TABLE [dbo].[MediaDeletionJobs] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [ImageUrl] NVARCHAR(1000) NOT NULL,
        [PublicId] NVARCHAR(255) NULL,
        [Status] INT NOT NULL DEFAULT 0,              -- 0: Pending, 1: Processing, 2: Completed, 3: Failed
        [RetryCount] INT NOT NULL DEFAULT 0,
        [MaxRetries] INT NOT NULL DEFAULT 5,
        [LastError] NVARCHAR(2000) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [ProcessedAt] DATETIME2 NULL,
        [NextRetryUtc] DATETIME2 NULL,
        [LockedBy] NVARCHAR(100) NULL,
        [LockedAtUtc] DATETIME2 NULL,

        CONSTRAINT [PK_MediaDeletionJobs] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    PRINT 'Creating indexes on [dbo].[MediaDeletionJobs]...';

    -- Index for retrieving due pending jobs efficiently
    CREATE NONCLUSTERED INDEX [IX_MediaDeletionJobs_Status_NextRetry]
    ON [dbo].[MediaDeletionJobs] ([Status], [NextRetryUtc])
    INCLUDE ([ImageUrl], [RetryCount], [LockedAtUtc]);

    -- Index for fast lookup by ImageUrl
    CREATE NONCLUSTERED INDEX [IX_MediaDeletionJobs_ImageUrl]
    ON [dbo].[MediaDeletionJobs] ([ImageUrl]);

    -- Unique filtered index preventing duplicate active (Pending or Processing) deletion jobs
    CREATE UNIQUE NONCLUSTERED INDEX [UX_MediaDeletionJobs_ActiveImageUrl]
    ON [dbo].[MediaDeletionJobs] ([ImageUrl])
    WHERE [Status] IN (0, 1);

    PRINT 'Migration completed successfully: [dbo].[MediaDeletionJobs] created.';
END
ELSE
BEGIN
    PRINT 'Table [dbo].[MediaDeletionJobs] already exists. Skipping creation.';
END
GO
