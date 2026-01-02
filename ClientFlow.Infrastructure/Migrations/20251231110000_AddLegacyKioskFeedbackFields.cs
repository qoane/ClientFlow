using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientFlow.Infrastructure.Migrations
{
    [Migration("20251231110000_AddLegacyKioskFeedbackFields")]
    public partial class AddLegacyKioskFeedbackFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ServiceType",
                table: "KioskFeedback",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "KioskFeedback",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgeRange",
                table: "KioskFeedback",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "KioskFeedback",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PoliciesJson",
                table: "KioskFeedback",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPreference",
                table: "KioskFeedback",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "KioskFeedback",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecommendRating",
                table: "KioskFeedback",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM [Surveys] WHERE [Code] = N'legacy')
                BEGIN
                    INSERT INTO [Surveys] ([Id], [Code], [Title], [Description], [IsActive])
                    VALUES ('cccccccc-cccc-cccc-cccc-cccccccccccc', N'legacy', N'Legacy', N'Legacy kiosk feedback', 1);
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM [Surveys]
                WHERE [Code] = N'legacy';
                """);

            migrationBuilder.DropColumn(
                name: "ServiceType",
                table: "KioskFeedback");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "KioskFeedback");

            migrationBuilder.DropColumn(
                name: "AgeRange",
                table: "KioskFeedback");

            migrationBuilder.DropColumn(
                name: "City",
                table: "KioskFeedback");

            migrationBuilder.DropColumn(
                name: "PoliciesJson",
                table: "KioskFeedback");

            migrationBuilder.DropColumn(
                name: "ContactPreference",
                table: "KioskFeedback");

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "KioskFeedback");

            migrationBuilder.DropColumn(
                name: "RecommendRating",
                table: "KioskFeedback");
        }
    }
}
