using ClientFlow.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientFlow.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20251115120000_EnsureSurveyBrandingColumns")]
    public partial class EnsureSurveyBrandingColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Surveys','ThemeAccent') IS NULL
BEGIN
    ALTER TABLE dbo.Surveys ADD ThemeAccent NVARCHAR(16) NULL;
END

IF COL_LENGTH('dbo.Surveys','ThemePanel') IS NULL
BEGIN
    ALTER TABLE dbo.Surveys ADD ThemePanel NVARCHAR(16) NULL;
END

IF COL_LENGTH('dbo.Surveys','CustomCss') IS NULL
BEGIN
    ALTER TABLE dbo.Surveys ADD CustomCss NVARCHAR(MAX) NULL;
END

IF COL_LENGTH('dbo.Surveys','PublishedVersion') IS NULL
BEGIN
    ALTER TABLE dbo.Surveys ADD PublishedVersion INT NULL;
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Surveys','PublishedVersion') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Surveys DROP COLUMN PublishedVersion;
END

IF COL_LENGTH('dbo.Surveys','CustomCss') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Surveys DROP COLUMN CustomCss;
END

IF COL_LENGTH('dbo.Surveys','ThemePanel') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Surveys DROP COLUMN ThemePanel;
END

IF COL_LENGTH('dbo.Surveys','ThemeAccent') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Surveys DROP COLUMN ThemeAccent;
END
");
        }
    }
}
