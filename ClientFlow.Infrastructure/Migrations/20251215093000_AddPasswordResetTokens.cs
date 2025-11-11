using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientFlow.Infrastructure.Migrations
{
    public partial class AddPasswordResetTokens : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Users','MustChangePassword') IS NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.Users ADD MustChangePassword BIT NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT(0);');
    EXEC(N'UPDATE dbo.Users SET MustChangePassword = 0 WHERE MustChangePassword IS NULL;');
    EXEC(N'ALTER TABLE dbo.Users DROP CONSTRAINT DF_Users_MustChangePassword;');
END

IF OBJECT_ID(N'dbo.PasswordResetTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetTokens
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PasswordResetTokens_Id DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        CodeHash NVARCHAR(256) NOT NULL,
        CreatedUtc DATETIME2 NOT NULL,
        ExpiresUtc DATETIME2 NOT NULL,
        IsUsed BIT NOT NULL CONSTRAINT DF_PasswordResetTokens_IsUsed DEFAULT(0),
        Purpose INT NOT NULL,
        CONSTRAINT PK_PasswordResetTokens PRIMARY KEY (Id)
    );

    CREATE INDEX IX_PasswordResetTokens_UserId ON dbo.PasswordResetTokens(UserId);

    ALTER TABLE dbo.PasswordResetTokens WITH CHECK
        ADD CONSTRAINT FK_PasswordResetTokens_Users_UserId
        FOREIGN KEY(UserId) REFERENCES dbo.Users(Id)
        ON DELETE CASCADE;
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.PasswordResetTokens', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PasswordResetTokens_Users_UserId')
        ALTER TABLE dbo.PasswordResetTokens DROP CONSTRAINT FK_PasswordResetTokens_Users_UserId;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PasswordResetTokens_UserId' AND object_id = OBJECT_ID('dbo.PasswordResetTokens'))
        DROP INDEX IX_PasswordResetTokens_UserId ON dbo.PasswordResetTokens;
    DROP TABLE dbo.PasswordResetTokens;
END

IF COL_LENGTH('dbo.Users','MustChangePassword') IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.Users DROP COLUMN MustChangePassword;');
END
");
        }
    }
}
