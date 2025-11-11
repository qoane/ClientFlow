using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameSessionsToTblSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Sessions",
                table: "Sessions");

            migrationBuilder.RenameTable(
                name: "Sessions",
                newName: "tblSessions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tblSessions",
                table: "tblSessions",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_tblSessions",
                table: "tblSessions");

            migrationBuilder.RenameTable(
                name: "tblSessions",
                newName: "Sessions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Sessions",
                table: "Sessions",
                column: "Id");
        }
    }
}
