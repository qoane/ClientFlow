using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReEnsureMustChangePasswordColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Users','MustChangePassword') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD MustChangePassword BIT NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT(0);
    UPDATE dbo.Users SET MustChangePassword = 0 WHERE MustChangePassword IS NULL;
    ALTER TABLE dbo.Users DROP CONSTRAINT DF_Users_MustChangePassword;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Users','MustChangePassword') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Users DROP COLUMN MustChangePassword;
END
");
        }
    }
}
