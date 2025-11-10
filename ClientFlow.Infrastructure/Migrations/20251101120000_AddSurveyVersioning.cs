using System;
using ClientFlow.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20251101120000_AddSurveyVersioning")]
    public partial class AddSurveyVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PublishedVersion",
                table: "Surveys",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SurveyVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurveyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyVersions_SurveyId_Version",
                table: "SurveyVersions",
                columns: new[] { "SurveyId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SurveyVersions");

            migrationBuilder.DropColumn(
                name: "PublishedVersion",
                table: "Surveys");
        }
    }
}
