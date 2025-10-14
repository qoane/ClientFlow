using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ClientFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KioskFeedbackAndSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Ensure Staff table exists (don’t recreate; add missing columns if needed)
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.Staff', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Staff(
        Id UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(MAX) NOT NULL,
        PhotoUrl NVARCHAR(MAX) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Staff_IsActive DEFAULT(1),
        CONSTRAINT PK_Staff PRIMARY KEY (Id)
    );
END
ELSE
BEGIN
    -- Add columns introduced by the new model if they are missing
    IF COL_LENGTH('dbo.Staff','PhotoUrl') IS NULL
        ALTER TABLE dbo.Staff ADD PhotoUrl NVARCHAR(MAX) NULL;

    IF COL_LENGTH('dbo.Staff','IsActive') IS NULL
    BEGIN
        ALTER TABLE dbo.Staff ADD IsActive BIT NOT NULL CONSTRAINT DF_Staff_IsActive DEFAULT(1);
        -- Backfill default for existing rows
        UPDATE dbo.Staff SET IsActive = 1 WHERE IsActive IS NULL;
    END
END
");

            // 2) Ensure Settings table exists
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.Settings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Settings(
        Id UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_Settings_Id DEFAULT NEWID(),
        [Key] NVARCHAR(256) NOT NULL,
        [Value] NVARCHAR(MAX) NULL,
        CONSTRAINT PK_Settings PRIMARY KEY (Id),
        CONSTRAINT UQ_Settings_Key UNIQUE ([Key])
    );
END
");

            // 3) Ensure KioskFeedback table exists
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.KioskFeedback', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KioskFeedback(
        Id UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_KioskFeedback_Id DEFAULT NEWID(),
        CreatedUtc DATETIME2 NOT NULL CONSTRAINT DF_KioskFeedback_Created DEFAULT (SYSUTCDATETIME()),
        Phone NVARCHAR(64) NULL,
        StaffId UNIQUEIDENTIFIER NOT NULL,
        TimeRating INT NOT NULL,
        RespectRating INT NOT NULL,
        OverallRating INT NOT NULL,
        StartedUtc DATETIME2 NULL,
        DurationSeconds INT NULL,
        Branch NVARCHAR(128) NULL,
        CONSTRAINT PK_KioskFeedback PRIMARY KEY (Id)
    );
END
");

            // 4) Ensure index + FK on KioskFeedback(StaffId)
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_KioskFeedback_StaffId' AND object_id = OBJECT_ID('dbo.KioskFeedback'))
BEGIN
    CREATE INDEX IX_KioskFeedback_StaffId ON dbo.KioskFeedback(StaffId);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_KioskFeedback_Staff_StaffId')
BEGIN
    ALTER TABLE dbo.KioskFeedback  WITH CHECK
    ADD CONSTRAINT FK_KioskFeedback_Staff_StaffId
    FOREIGN KEY(StaffId) REFERENCES dbo.Staff(Id) ON DELETE NO ACTION;
END
");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop KioskFeedback (safe to drop first due to FK)
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.KioskFeedback', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_KioskFeedback_Staff_StaffId')
        ALTER TABLE dbo.KioskFeedback DROP CONSTRAINT FK_KioskFeedback_Staff_StaffId;

    DROP TABLE dbo.KioskFeedback;
END
");

            // Drop Settings (optional; Staff is left intact intentionally)
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.Settings', N'U') IS NOT NULL
    DROP TABLE dbo.Settings;
");
        }

    }
}
