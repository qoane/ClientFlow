using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsureSettingsJsonColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Questions', 'SettingsJson') IS NULL
BEGIN
    ALTER TABLE [dbo].[Questions]
    ADD [SettingsJson] nvarchar(max) NULL;
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Sections', 'SettingsJson') IS NULL
BEGIN
    ALTER TABLE [dbo].[Sections]
    ADD [SettingsJson] nvarchar(max) NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Questions', 'SettingsJson') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Questions]
    DROP COLUMN [SettingsJson];
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Sections', 'SettingsJson') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Sections]
    DROP COLUMN [SettingsJson];
END
");
        }
    }
}
