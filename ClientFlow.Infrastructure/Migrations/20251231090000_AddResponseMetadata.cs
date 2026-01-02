using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientFlow.Infrastructure.Migrations
{
    [Migration("20251231090000_AddResponseMetadata")]
    public partial class AddResponseMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedUtc",
                table: "Responses",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "Responses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientCode",
                table: "Responses",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormKey",
                table: "Responses",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartedUtc",
                table: "Responses");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "Responses");

            migrationBuilder.DropColumn(
                name: "ClientCode",
                table: "Responses");

            migrationBuilder.DropColumn(
                name: "FormKey",
                table: "Responses");
        }
    }
}
