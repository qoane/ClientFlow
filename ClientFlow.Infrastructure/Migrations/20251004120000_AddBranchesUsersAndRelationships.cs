using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchesUsersAndRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure Branches table exists with required schema
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.Branches', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Branches(
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Branches_Id DEFAULT NEWID(),
        Name NVARCHAR(256) NOT NULL,
        ReportRecipients NVARCHAR(MAX) NULL,
        ReportTime NVARCHAR(MAX) NULL,
        CONSTRAINT PK_Branches PRIMARY KEY (Id)
    );
    CREATE UNIQUE INDEX IX_Branches_Name ON dbo.Branches(Name);
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.Branches','ReportRecipients') IS NULL
        ALTER TABLE dbo.Branches ADD ReportRecipients NVARCHAR(MAX) NULL;

    IF COL_LENGTH('dbo.Branches','ReportTime') IS NULL
        ALTER TABLE dbo.Branches ADD ReportTime NVARCHAR(MAX) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Branches_Name' AND object_id = OBJECT_ID('dbo.Branches'))
        CREATE UNIQUE INDEX IX_Branches_Name ON dbo.Branches(Name);
END");

            // Ensure default branch seed exists
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.Branches', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE Id = '00000000-0000-0000-0000-000000000001')
    BEGIN
        INSERT INTO dbo.Branches (Id, Name, ReportRecipients, ReportTime)
        VALUES ('00000000-0000-0000-0000-000000000001', 'Head Office', '', '08:00');
    END
END");

            // Ensure Users table exists and includes CreatedByUserId + BranchId relationships
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users(
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Users_Id DEFAULT NEWID(),
        Email NVARCHAR(256) NOT NULL,
        PasswordHash NVARCHAR(MAX) NOT NULL,
        Role INT NOT NULL,
        BranchId UNIQUEIDENTIFIER NULL,
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        CONSTRAINT PK_Users PRIMARY KEY (Id)
    );
    CREATE UNIQUE INDEX IX_Users_Email ON dbo.Users(Email);
    CREATE INDEX IX_Users_BranchId ON dbo.Users(BranchId);
    CREATE INDEX IX_Users_CreatedByUserId ON dbo.Users(CreatedByUserId);
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.Users','Email') IS NULL
        ALTER TABLE dbo.Users ADD Email NVARCHAR(256) NOT NULL CONSTRAINT DF_Users_Email DEFAULT('');

    IF COL_LENGTH('dbo.Users','PasswordHash') IS NULL
    BEGIN
        ALTER TABLE dbo.Users ADD PasswordHash NVARCHAR(MAX) NULL;
        UPDATE dbo.Users SET PasswordHash = '' WHERE PasswordHash IS NULL;
        ALTER TABLE dbo.Users ALTER COLUMN PasswordHash NVARCHAR(MAX) NOT NULL;
    END

    IF COL_LENGTH('dbo.Users','Role') IS NULL
    BEGIN
        ALTER TABLE dbo.Users ADD Role INT NULL;
        UPDATE dbo.Users SET Role = 0 WHERE Role IS NULL;
        ALTER TABLE dbo.Users ALTER COLUMN Role INT NOT NULL;
    END

    IF COL_LENGTH('dbo.Users','BranchId') IS NULL
        ALTER TABLE dbo.Users ADD BranchId UNIQUEIDENTIFIER NULL;

    IF COL_LENGTH('dbo.Users','CreatedByUserId') IS NULL
        ALTER TABLE dbo.Users ADD CreatedByUserId UNIQUEIDENTIFIER NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Email' AND object_id = OBJECT_ID('dbo.Users'))
        CREATE UNIQUE INDEX IX_Users_Email ON dbo.Users(Email);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_BranchId' AND object_id = OBJECT_ID('dbo.Users'))
        CREATE INDEX IX_Users_BranchId ON dbo.Users(BranchId);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_CreatedByUserId' AND object_id = OBJECT_ID('dbo.Users'))
        CREATE INDEX IX_Users_CreatedByUserId ON dbo.Users(CreatedByUserId);
END");

            // Ensure default super admin account exists
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = '11111111-0000-0000-0000-000000000000')
    BEGIN
        INSERT INTO dbo.Users (Id, Email, PasswordHash, Role, BranchId, CreatedByUserId)
        VALUES ('11111111-0000-0000-0000-000000000000', 'admin@example.com', 'PrP+ZrMeO00Q+nC1ytSccRIpSvauTkdqHEBRVdRaoSE=', 2, NULL, NULL);
    END
END");

            // Ensure Staff table has BranchId column and FK to Branches
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.Staff', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Staff','BranchId') IS NULL
    BEGIN
        ALTER TABLE dbo.Staff ADD BranchId UNIQUEIDENTIFIER NULL;
        UPDATE dbo.Staff SET BranchId = '00000000-0000-0000-0000-000000000001' WHERE BranchId IS NULL;
    END

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Staff_BranchId' AND object_id = OBJECT_ID('dbo.Staff'))
        CREATE INDEX IX_Staff_BranchId ON dbo.Staff(BranchId);
END");

            // Ensure KioskFeedback table has Branch relationship columns
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.KioskFeedback', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.KioskFeedback','BranchId') IS NULL
    BEGIN
        ALTER TABLE dbo.KioskFeedback ADD BranchId UNIQUEIDENTIFIER NULL;
        UPDATE dbo.KioskFeedback SET BranchId = '00000000-0000-0000-0000-000000000001' WHERE BranchId IS NULL;
        ALTER TABLE dbo.KioskFeedback ALTER COLUMN BranchId UNIQUEIDENTIFIER NOT NULL;
    END

    IF COL_LENGTH('dbo.KioskFeedback','BranchName') IS NULL
        ALTER TABLE dbo.KioskFeedback ADD BranchName NVARCHAR(MAX) NULL;

    IF COL_LENGTH('dbo.KioskFeedback','Branch') IS NOT NULL
    BEGIN
        UPDATE dbo.KioskFeedback SET BranchName = Branch WHERE BranchName IS NULL;
        ALTER TABLE dbo.KioskFeedback DROP COLUMN Branch;
    END
END");

            // Add foreign key constraints for new relationships
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Branches', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_Branches_BranchId')
    BEGIN
        ALTER TABLE dbo.Users  WITH CHECK
        ADD CONSTRAINT FK_Users_Branches_BranchId
        FOREIGN KEY(BranchId) REFERENCES dbo.Branches(Id)
        ON DELETE SET NULL;
    END
END");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_Users_CreatedByUserId')
    BEGIN
        ALTER TABLE dbo.Users  WITH CHECK
        ADD CONSTRAINT FK_Users_Users_CreatedByUserId
        FOREIGN KEY(CreatedByUserId) REFERENCES dbo.Users(Id)
        ON DELETE NO ACTION;
    END
END");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.Staff', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Branches', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Staff_Branches_BranchId')
    BEGIN
        ALTER TABLE dbo.Staff  WITH CHECK
        ADD CONSTRAINT FK_Staff_Branches_BranchId
        FOREIGN KEY(BranchId) REFERENCES dbo.Branches(Id)
        ON DELETE SET NULL;
    END
END");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.KioskFeedback', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Branches', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_KioskFeedback_Branches_BranchId')
    BEGIN
        ALTER TABLE dbo.KioskFeedback  WITH CHECK
        ADD CONSTRAINT FK_KioskFeedback_Branches_BranchId
        FOREIGN KEY(BranchId) REFERENCES dbo.Branches(Id)
        ON DELETE NO ACTION;
    END
END");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.KioskFeedback', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_KioskFeedback_BranchId' AND object_id = OBJECT_ID('dbo.KioskFeedback'))
        CREATE INDEX IX_KioskFeedback_BranchId ON dbo.KioskFeedback(BranchId);
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.KioskFeedback', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_KioskFeedback_Branches_BranchId')
        ALTER TABLE dbo.KioskFeedback DROP CONSTRAINT FK_KioskFeedback_Branches_BranchId;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_KioskFeedback_BranchId' AND object_id = OBJECT_ID('dbo.KioskFeedback'))
        DROP INDEX IX_KioskFeedback_BranchId ON dbo.KioskFeedback;

    IF COL_LENGTH('dbo.KioskFeedback','Branch') IS NULL
        ALTER TABLE dbo.KioskFeedback ADD Branch NVARCHAR(MAX) NULL;

    IF COL_LENGTH('dbo.KioskFeedback','BranchName') IS NOT NULL
    BEGIN
        UPDATE dbo.KioskFeedback SET Branch = BranchName WHERE Branch IS NULL;
        ALTER TABLE dbo.KioskFeedback DROP COLUMN BranchName;
    END

    IF COL_LENGTH('dbo.KioskFeedback','BranchId') IS NOT NULL
        ALTER TABLE dbo.KioskFeedback DROP COLUMN BranchId;
END");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.Staff', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Staff_Branches_BranchId')
        ALTER TABLE dbo.Staff DROP CONSTRAINT FK_Staff_Branches_BranchId;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Staff_BranchId' AND object_id = OBJECT_ID('dbo.Staff'))
        DROP INDEX IX_Staff_BranchId ON dbo.Staff;

    IF COL_LENGTH('dbo.Staff','BranchId') IS NOT NULL
        ALTER TABLE dbo.Staff DROP COLUMN BranchId;
END");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_Users_CreatedByUserId')
        ALTER TABLE dbo.Users DROP CONSTRAINT FK_Users_Users_CreatedByUserId;

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_Branches_BranchId')
        ALTER TABLE dbo.Users DROP CONSTRAINT FK_Users_Branches_BranchId;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Email' AND object_id = OBJECT_ID('dbo.Users'))
        DROP INDEX IX_Users_Email ON dbo.Users;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_BranchId' AND object_id = OBJECT_ID('dbo.Users'))
        DROP INDEX IX_Users_BranchId ON dbo.Users;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_CreatedByUserId' AND object_id = OBJECT_ID('dbo.Users'))
        DROP INDEX IX_Users_CreatedByUserId ON dbo.Users;

    IF COL_LENGTH('dbo.Users','CreatedByUserId') IS NOT NULL
        ALTER TABLE dbo.Users DROP COLUMN CreatedByUserId;

    IF COL_LENGTH('dbo.Users','BranchId') IS NOT NULL
        ALTER TABLE dbo.Users DROP COLUMN BranchId;

    IF COL_LENGTH('dbo.Users','Role') IS NOT NULL
        ALTER TABLE dbo.Users DROP COLUMN Role;

    IF COL_LENGTH('dbo.Users','PasswordHash') IS NOT NULL
        ALTER TABLE dbo.Users DROP COLUMN PasswordHash;

    IF COL_LENGTH('dbo.Users','Email') IS NOT NULL
        ALTER TABLE dbo.Users DROP COLUMN Email;
END");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Branches_Name' AND object_id = OBJECT_ID('dbo.Branches'))
    DROP INDEX IX_Branches_Name ON dbo.Branches;

IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
    DROP TABLE dbo.Users;

IF OBJECT_ID(N'dbo.Branches', N'U') IS NOT NULL
    DROP TABLE dbo.Branches;
");
        }
    }
}
