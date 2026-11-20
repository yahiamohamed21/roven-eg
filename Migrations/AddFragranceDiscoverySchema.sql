BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141140_AddFragranceDiscoverySchema'
)
BEGIN
    ALTER TABLE [Products] ADD [FragranceProfileReviewedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141140_AddFragranceDiscoverySchema'
)
BEGIN
    ALTER TABLE [Products] ADD [InspiredBy] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141140_AddFragranceDiscoverySchema'
)
BEGIN
    ALTER TABLE [Products] ADD [IsFragranceProfileApproved] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141140_AddFragranceDiscoverySchema'
)
BEGIN
    ALTER TABLE [Products] ADD [Projection] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141140_AddFragranceDiscoverySchema'
)
BEGIN
    CREATE TABLE [FragranceNotes] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [ArabicName] nvarchar(100) NOT NULL,
        [Category] int NOT NULL,
        [IsActive] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_FragranceNotes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141140_AddFragranceDiscoverySchema'
)
BEGIN
    CREATE TABLE [ProductSeasons] (
        [ProductId] int NOT NULL,
        [Season] int NOT NULL,
        CONSTRAINT [PK_ProductSeasons] PRIMARY KEY ([ProductId], [Season]),
        CONSTRAINT [FK_ProductSeasons_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141140_AddFragranceDiscoverySchema'
)
BEGIN
    CREATE TABLE [ProductTargetAudiences] (
        [ProductId] int NOT NULL,
        [Audience] int NOT NULL,
        CONSTRAINT [PK_ProductTargetAudiences] PRIMARY KEY ([ProductId], [Audience]),
        CONSTRAINT [FK_ProductTargetAudiences_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141140_AddFragranceDiscoverySchema'
)
BEGIN
    CREATE TABLE [FragranceNoteQuickPicks] (
        [FragranceNoteId] int NOT NULL,
        [Context] int NOT NULL,
        [DisplayOrder] int NOT NULL,
        CONSTRAINT [PK_FragranceNoteQuickPicks] PRIMARY KEY ([FragranceNoteId], [Context]),
        CONSTRAINT [FK_FragranceNoteQuickPicks_FragranceNotes_FragranceNoteId] FOREIGN KEY ([FragranceNoteId]) REFERENCES [FragranceNotes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141140_AddFragranceDiscoverySchema'
)
BEGIN
    CREATE TABLE [ProductFragranceNotes] (
        [ProductId] int NOT NULL,
        [FragranceNoteId] int NOT NULL,
        [AddedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ProductFragranceNotes] PRIMARY KEY ([ProductId], [FragranceNoteId]),
        CONSTRAINT [FK_ProductFragranceNotes_FragranceNotes_FragranceNoteId] FOREIGN KEY ([FragranceNoteId]) REFERENCES [FragranceNotes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductFragranceNotes_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141140_AddFragranceDiscoverySchema'
)
BEGIN
    CREATE INDEX [IX_Products_IsFragranceProfileApproved] ON [Products] ([IsFragranceProfileApproved]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141140_AddFragranceDiscoverySchema'
)
BEGIN
    CREATE INDEX [IX_FragranceNoteQuickPicks_Context] ON [FragranceNoteQuickPicks] ([Context]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141140_AddFragranceDiscoverySchema'
)
BEGIN
    CREATE INDEX [IX_FragranceNoteQuickPicks_DisplayOrder] ON [FragranceNoteQuickPicks] ([DisplayOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141140_AddFragranceDiscoverySchema'
)
BEGIN
    CREATE INDEX [IX_FragranceNotes_Category] ON [FragranceNotes] ([Category]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141140_AddFragranceDiscoverySchema'
)
BEGIN
    CREATE INDEX [IX_FragranceNotes_DisplayOrder] ON [FragranceNotes] ([DisplayOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141140_AddFragranceDiscoverySchema'
)
BEGIN
    CREATE INDEX [IX_FragranceNotes_IsActive] ON [FragranceNotes] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141140_AddFragranceDiscoverySchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FragranceNotes_Name] ON [FragranceNotes] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141140_AddFragranceDiscoverySchema'
)
BEGIN
    CREATE INDEX [IX_ProductFragranceNotes_FragranceNoteId] ON [ProductFragranceNotes] ([FragranceNoteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120141140_AddFragranceDiscoverySchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261120141140_AddFragranceDiscoverySchema', N'9.0.2');
END;

COMMIT;
GO

