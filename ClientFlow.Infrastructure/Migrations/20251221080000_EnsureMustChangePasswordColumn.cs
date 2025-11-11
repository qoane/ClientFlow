using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsureMustChangePasswordColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Users','MustChangePassword') IS NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.Users ADD MustChangePassword BIT NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT(0);');
    EXEC(N'UPDATE dbo.Users SET MustChangePassword = 0 WHERE MustChangePassword IS NULL;');
    EXEC(N'ALTER TABLE dbo.Users DROP CONSTRAINT DF_Users_MustChangePassword;');
END
ELSE
BEGIN
    DECLARE @isNullable bit = (SELECT CASE WHEN is_nullable = 1 THEN 1 ELSE 0 END
        FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'MustChangePassword');
    IF (@isNullable = 1)
    BEGIN
        EXEC(N'UPDATE dbo.Users SET MustChangePassword = 0 WHERE MustChangePassword IS NULL;');
        EXEC(N'ALTER TABLE dbo.Users ALTER COLUMN MustChangePassword BIT NOT NULL;');
    END
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Users','MustChangePassword') IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.Users DROP COLUMN MustChangePassword;');
END");
        }
    }
}
