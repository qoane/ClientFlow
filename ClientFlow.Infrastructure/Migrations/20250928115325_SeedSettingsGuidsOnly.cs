using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ClientFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedSettingsGuidsOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
MERGE dbo.Settings AS tgt
USING (VALUES
  (CAST('40EAF4A3-5D07-4A5B-9C8D-6A2E6E3DC0F1' AS UNIQUEIDENTIFIER), N'ReportRecipients', N''),
  (CAST('6B7D2F8B-0F6F-4A5E-9F7C-0E8E2D2C9A11' AS UNIQUEIDENTIFIER), N'BranchName', N'Head Office'),
  (CAST('E2C4B8E9-1A3D-4E8F-9C0B-3F2E6D7A9B55' AS UNIQUEIDENTIFIER), N'ReportTime', N'08:00')
) AS src(Id, [Key], [Value])
ON tgt.[Key] = src.[Key]
WHEN MATCHED THEN
    UPDATE SET tgt.[Value] = src.[Value]
WHEN NOT MATCHED THEN
    INSERT (Id,[Key],[Value]) VALUES (src.Id, src.[Key], src.[Value]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM dbo.Settings WHERE [Key] IN (N'ReportRecipients', N'BranchName', N'ReportTime');
");


        }
    }
}
