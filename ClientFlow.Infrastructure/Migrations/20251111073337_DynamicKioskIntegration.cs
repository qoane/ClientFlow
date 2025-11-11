using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DynamicKioskIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartedUtc",
                table: "KioskFeedback",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedUtc",
                table: "KioskFeedback",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                column: "Value",
                value: "11111111-1111-1111-1111-111111111111");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                column: "Value",
                value: "22222222-2222-2222-2222-222222222222");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                column: "Value",
                value: "33333333-3333-3333-3333-333333333333");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"),
                column: "Value",
                value: "44444444-4444-4444-4444-444444444444");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000301"),
                column: "Value",
                value: "1");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000302"),
                column: "Value",
                value: "2");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000303"),
                column: "Value",
                value: "3");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000304"),
                column: "Value",
                value: "4");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000305"),
                column: "Value",
                value: "5");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000401"),
                column: "Value",
                value: "1");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000402"),
                column: "Value",
                value: "2");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000403"),
                column: "Value",
                value: "3");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000404"),
                column: "Value",
                value: "4");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000405"),
                column: "Value",
                value: "5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "StartedUtc",
                table: "KioskFeedback",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedUtc",
                table: "KioskFeedback",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                column: "Value",
                value: "neo-ramohabi");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                column: "Value",
                value: "baradi-boikanyo");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                column: "Value",
                value: "tsepo-chefa");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"),
                column: "Value",
                value: "mpho-phalafang");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000301"),
                column: "Value",
                value: "not-at-all");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000302"),
                column: "Value",
                value: "no");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000303"),
                column: "Value",
                value: "neutral");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000304"),
                column: "Value",
                value: "yes-mostly");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000305"),
                column: "Value",
                value: "absolutely");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000401"),
                column: "Value",
                value: "not-at-all");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000402"),
                column: "Value",
                value: "no");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000403"),
                column: "Value",
                value: "neutral");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000404"),
                column: "Value",
                value: "yes-mostly");

            migrationBuilder.UpdateData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000405"),
                column: "Value",
                value: "absolutely");
        }
    }
}
