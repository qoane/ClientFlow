using System;
using ClientFlow.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20251010120000_UpdateLibertySurvey")]
    public partial class UpdateLibertySurvey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM [Questions]
                WHERE [Id] IN ('cccccccc-cccc-cccc-cccc-cccccccccccc', 'dddddddd-dddd-dddd-dddd-dddddddddddd');
                """);

            migrationBuilder.DeleteData(
                table: "Sections",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

            migrationBuilder.InsertData(
                table: "Sections",
                columns: new[] { "Id", "Columns", "Order", "SurveyId", "Title" },
                values: new object[,]
                {
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), 1, 1, new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Client Identification" },
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbc"), 1, 2, new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Service Interaction" },
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbd"), 1, 3, new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Core Satisfaction Questions" },
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbe"), 1, 4, new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Feedback & Follow-Up" },
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbf"), 1, 5, new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "End Screen" }
                });

            migrationBuilder.Sql(
                """
                INSERT INTO [Questions] ([Id], [Key], [Order], [Prompt], [Required], [SectionId], [SettingsJson], [SurveyId], [Type])
                VALUES
                    ('10000000-0000-0000-0000-000000000001', 'phone', 1, N'Please enter your phone number.', 1, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', NULL, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'phone'),
                    ('10000000-0000-0000-0000-000000000002', 'staff', 2, N'I was assisted by…', 1, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbc', NULL, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'radio'),
                    ('10000000-0000-0000-0000-000000000003', 'service', 3, N'Which Liberty service were you assisted with today?', 1, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbc', NULL, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'radio'),
                    ('10000000-0000-0000-0000-000000000004', 'satisfaction', 4, N'How satisfied are you with your recent interaction with Liberty?', 1, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbd', NULL, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'radio'),
                    ('10000000-0000-0000-0000-000000000005', 'timeliness', 5, N'Was your query handled within a reasonable time?', 1, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbd', NULL, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'radio'),
                    ('10000000-0000-0000-0000-000000000006', 'professionalism', 6, N'Did our staff treat you professionally and with respect?', 1, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbd', NULL, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'radio'),
                    ('10000000-0000-0000-0000-000000000007', 'recommend', 7, N'How likely are you to recommend Liberty to a friend or family member?', 1, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbd', NULL, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'radio'),
                    ('10000000-0000-0000-0000-000000000008', 'follow_up', 8, N'Would you like someone to contact you about your experience?', 1, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbe', NULL, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'radio'),
                    ('10000000-0000-0000-0000-000000000009', 'improvement', 9, N'Please tell us briefly what we could do better.', 0, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbe', N'{"placeholder":"Your feedback helps us improve."}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'text'),
                    ('10000000-0000-0000-0000-00000000000A', 'end', 10, N'Thank you for your feedback! Your input helps us improve our service.', 0, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbf', NULL, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'message');
                """);

            migrationBuilder.InsertData(
                table: "Options",
                columns: new[] { "Id", "Label", "Order", "QuestionId", "Value" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), "Neo Ramohabi", 1, new Guid("10000000-0000-0000-0000-000000000002"), "neo-ramohabi" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "Baradi Boikanyo", 2, new Guid("10000000-0000-0000-0000-000000000002"), "baradi-boikanyo" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "Tsepo Chefa", 3, new Guid("10000000-0000-0000-0000-000000000002"), "tsepo-chefa" },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "Mpho Phalafang", 4, new Guid("10000000-0000-0000-0000-000000000002"), "mpho-phalafang" },
                    { new Guid("20000000-0000-0000-0000-000000000101"), "Policy enquiry", 1, new Guid("10000000-0000-0000-0000-000000000003"), "policy-enquiry" },
                    { new Guid("20000000-0000-0000-0000-000000000102"), "Claim", 2, new Guid("10000000-0000-0000-0000-000000000003"), "claim" },
                    { new Guid("20000000-0000-0000-0000-000000000103"), "New policy application", 3, new Guid("10000000-0000-0000-0000-000000000003"), "new-policy-application" },
                    { new Guid("20000000-0000-0000-0000-000000000104"), "Payment", 4, new Guid("10000000-0000-0000-0000-000000000003"), "payment" },
                    { new Guid("20000000-0000-0000-0000-000000000105"), "Amendments/ Policy Changes", 5, new Guid("10000000-0000-0000-0000-000000000003"), "amendments" },
                    { new Guid("20000000-0000-0000-0000-000000000106"), "Other", 6, new Guid("10000000-0000-0000-0000-000000000003"), "other" },
                    { new Guid("20000000-0000-0000-0000-000000000201"), "😊 1 = Excellent", 1, new Guid("10000000-0000-0000-0000-000000000004"), "1" },
                    { new Guid("20000000-0000-0000-0000-000000000202"), "😃 2 = Good", 2, new Guid("10000000-0000-0000-0000-000000000004"), "2" },
                    { new Guid("20000000-0000-0000-0000-000000000203"), "😐 3 = Okay", 3, new Guid("10000000-0000-0000-0000-000000000004"), "3" },
                    { new Guid("20000000-0000-0000-0000-000000000204"), "😞 4 = Poor", 4, new Guid("10000000-0000-0000-0000-000000000004"), "4" },
                    { new Guid("20000000-0000-0000-0000-000000000205"), "😡 5 = Very Poor", 5, new Guid("10000000-0000-0000-0000-000000000004"), "5" },
                    { new Guid("20000000-0000-0000-0000-000000000301"), "Not at all", 1, new Guid("10000000-0000-0000-0000-000000000005"), "not-at-all" },
                    { new Guid("20000000-0000-0000-0000-000000000302"), "No", 2, new Guid("10000000-0000-0000-0000-000000000005"), "no" },
                    { new Guid("20000000-0000-0000-0000-000000000303"), "Neutral", 3, new Guid("10000000-0000-0000-0000-000000000005"), "neutral" },
                    { new Guid("20000000-0000-0000-0000-000000000304"), "Yes, mostly", 4, new Guid("10000000-0000-0000-0000-000000000005"), "yes-mostly" },
                    { new Guid("20000000-0000-0000-0000-000000000305"), "Absolutely", 5, new Guid("10000000-0000-0000-0000-000000000005"), "absolutely" },
                    { new Guid("20000000-0000-0000-0000-000000000401"), "Not at all", 1, new Guid("10000000-0000-0000-0000-000000000006"), "not-at-all" },
                    { new Guid("20000000-0000-0000-0000-000000000402"), "No", 2, new Guid("10000000-0000-0000-0000-000000000006"), "no" },
                    { new Guid("20000000-0000-0000-0000-000000000403"), "Neutral", 3, new Guid("10000000-0000-0000-0000-000000000006"), "neutral" },
                    { new Guid("20000000-0000-0000-0000-000000000404"), "Yes, mostly", 4, new Guid("10000000-0000-0000-0000-000000000006"), "yes-mostly" },
                    { new Guid("20000000-0000-0000-0000-000000000405"), "Absolutely", 5, new Guid("10000000-0000-0000-0000-000000000006"), "absolutely" },
                    { new Guid("20000000-0000-0000-0000-000000000501"), "😊 1 = Extremely likely", 1, new Guid("10000000-0000-0000-0000-000000000007"), "1" },
                    { new Guid("20000000-0000-0000-0000-000000000502"), "😃 2 = Very likely", 2, new Guid("10000000-0000-0000-0000-000000000007"), "2" },
                    { new Guid("20000000-0000-0000-0000-000000000503"), "😐 3 = Neutral", 3, new Guid("10000000-0000-0000-0000-000000000007"), "3" },
                    { new Guid("20000000-0000-0000-0000-000000000504"), "😞 4 = Slightly likely", 4, new Guid("10000000-0000-0000-0000-000000000007"), "4" },
                    { new Guid("20000000-0000-0000-0000-000000000505"), "😡 5 = Not at all likely", 5, new Guid("10000000-0000-0000-0000-000000000007"), "5" },
                    { new Guid("20000000-0000-0000-0000-000000000601"), "Yes", 1, new Guid("10000000-0000-0000-0000-000000000008"), "yes" },
                    { new Guid("20000000-0000-0000-0000-000000000602"), "No", 2, new Guid("10000000-0000-0000-0000-000000000008"), "no" }
                });

            migrationBuilder.UpdateData(
                table: "Staff",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "Name",
                value: "Neo Ramohabi");

            migrationBuilder.UpdateData(
                table: "Staff",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "Name",
                value: "Tsepo Chefa");

            migrationBuilder.UpdateData(
                table: "Staff",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "Name",
                value: "Mpho Phalafang");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Staff",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "Name",
                value: "Neo Ramohlabi");

            migrationBuilder.UpdateData(
                table: "Staff",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "Name",
                value: "Ts'epo Chefa");

            migrationBuilder.UpdateData(
                table: "Staff",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "Name",
                value: "Mpho Phahlang");

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000101"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000102"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000103"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000104"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000105"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000106"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000201"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000202"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000203"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000204"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000205"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000301"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000302"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000303"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000304"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000305"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000401"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000402"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000403"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000404"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000405"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000501"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000502"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000503"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000504"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000505"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000601"));

            migrationBuilder.DeleteData(
                table: "Options",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000602"));

            migrationBuilder.Sql(
                """
                DELETE FROM [Questions]
                WHERE [Id] IN (
                    '10000000-0000-0000-0000-000000000001',
                    '10000000-0000-0000-0000-000000000002',
                    '10000000-0000-0000-0000-000000000003',
                    '10000000-0000-0000-0000-000000000004',
                    '10000000-0000-0000-0000-000000000005',
                    '10000000-0000-0000-0000-000000000006',
                    '10000000-0000-0000-0000-000000000007',
                    '10000000-0000-0000-0000-000000000008',
                    '10000000-0000-0000-0000-000000000009',
                    '10000000-0000-0000-0000-00000000000A'
                );
                """);

            migrationBuilder.DeleteData(
                table: "Sections",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

            migrationBuilder.DeleteData(
                table: "Sections",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbc"));

            migrationBuilder.DeleteData(
                table: "Sections",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbd"));

            migrationBuilder.DeleteData(
                table: "Sections",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbe"));

            migrationBuilder.DeleteData(
                table: "Sections",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbf"));

            migrationBuilder.InsertData(
                table: "Sections",
                columns: new[] { "Id", "Columns", "Order", "SurveyId", "Title" },
                values: new object[] { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), 1, 1, new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Main" });

            migrationBuilder.Sql(
                """
                INSERT INTO [Questions] ([Id], [Key], [Order], [Prompt], [Required], [SectionId], [SettingsJson], [SurveyId], [Type])
                VALUES
                    ('cccccccc-cccc-cccc-cccc-cccccccccccc', 'nps', 1, N'How likely are you to recommend Liberty to a friend or colleague?', 1, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', NULL, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'nps_0_10'),
                    ('dddddddd-dddd-dddd-dddd-dddddddddddd', 'reason', 2, N'What is the primary reason for your score?', 0, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', N'{"placeholder":"Tell us more…"}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'text');
                """);
        }
    }
}
