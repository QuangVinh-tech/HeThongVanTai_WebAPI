-- Phần còn thiếu của migration TannReview (AvatarUrl và Images đã có sẵn)
IF OBJECT_ID('dbo.Reviews') IS NULL
BEGIN
    CREATE TABLE [Reviews] (
        [Id] int NOT NULL IDENTITY,
        [TicketId] int NOT NULL,
        [TripId] int NOT NULL,
        [CustomerId] int NOT NULL,
        [Rating] int NOT NULL,
        [Comment] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reviews_Tickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_Reviews_TicketId] ON [Reviews] ([TicketId]);
    CREATE INDEX [IX_Reviews_TripId] ON [Reviews] ([TripId]);
END;

IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20261010032837_TannReview')
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20261010032837_TannReview', '8.0.31');

-- Kiểm tra cột bảng Images
SELECT c.name AS Cot, ty.name AS Kieu
FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.Images');