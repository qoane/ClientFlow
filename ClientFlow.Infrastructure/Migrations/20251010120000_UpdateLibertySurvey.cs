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

            migrationBuilder.Sql(
                """
                DELETE FROM [Sections]
                WHERE [Id] = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO [Sections] ([Id], [Columns], [Order], [SurveyId], [Title])
                VALUES
                    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 1, 1, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', N'Client Identification'),
                    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbc', 1, 2, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', N'Service Interaction'),
                    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbd', 1, 3, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', N'Core Satisfaction Questions'),
                    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbe', 1, 4, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', N'Feedback & Follow-Up'),
                    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbf', 1, 5, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', N'End Screen');
                """);

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

            migrationBuilder.Sql(
                """
                INSERT INTO [Options] ([Id], [Label], [Order], [QuestionId], [Value])
                VALUES
                    ('20000000-0000-0000-0000-000000000001', N'Neo Ramohabi', 1, '10000000-0000-0000-0000-000000000002', 'neo-ramohabi'),
                    ('20000000-0000-0000-0000-000000000002', N'Baradi Boikanyo', 2, '10000000-0000-0000-0000-000000000002', 'baradi-boikanyo'),
                    ('20000000-0000-0000-0000-000000000003', N'Tsepo Chefa', 3, '10000000-0000-0000-0000-000000000002', 'tsepo-chefa'),
                    ('20000000-0000-0000-0000-000000000004', N'Mpho Phalafang', 4, '10000000-0000-0000-0000-000000000002', 'mpho-phalafang'),
                    ('20000000-0000-0000-0000-000000000101', N'Policy enquiry', 1, '10000000-0000-0000-0000-000000000003', 'policy-enquiry'),
                    ('20000000-0000-0000-0000-000000000102', N'Claim', 2, '10000000-0000-0000-0000-000000000003', 'claim'),
                    ('20000000-0000-0000-0000-000000000103', N'New policy application', 3, '10000000-0000-0000-0000-000000000003', 'new-policy-application'),
                    ('20000000-0000-0000-0000-000000000104', N'Payment', 4, '10000000-0000-0000-0000-000000000003', 'payment'),
                    ('20000000-0000-0000-0000-000000000105', N'Amendments/ Policy Changes', 5, '10000000-0000-0000-0000-000000000003', 'amendments'),
                    ('20000000-0000-0000-0000-000000000106', N'Other', 6, '10000000-0000-0000-0000-000000000003', 'other'),
                    ('20000000-0000-0000-0000-000000000201', N'😊 1 = Excellent', 1, '10000000-0000-0000-0000-000000000004', '1'),
                    ('20000000-0000-0000-0000-000000000202', N'😃 2 = Good', 2, '10000000-0000-0000-0000-000000000004', '2'),
                    ('20000000-0000-0000-0000-000000000203', N'😐 3 = Okay', 3, '10000000-0000-0000-0000-000000000004', '3'),
                    ('20000000-0000-0000-0000-000000000204', N'😞 4 = Poor', 4, '10000000-0000-0000-0000-000000000004', '4'),
                    ('20000000-0000-0000-0000-000000000205', N'😡 5 = Very Poor', 5, '10000000-0000-0000-0000-000000000004', '5'),
                    ('20000000-0000-0000-0000-000000000301', N'Not at all', 1, '10000000-0000-0000-0000-000000000005', 'not-at-all'),
                    ('20000000-0000-0000-0000-000000000302', N'No', 2, '10000000-0000-0000-0000-000000000005', 'no'),
                    ('20000000-0000-0000-0000-000000000303', N'Neutral', 3, '10000000-0000-0000-0000-000000000005', 'neutral'),
                    ('20000000-0000-0000-0000-000000000304', N'Yes, mostly', 4, '10000000-0000-0000-0000-000000000005', 'yes-mostly'),
                    ('20000000-0000-0000-0000-000000000305', N'Absolutely', 5, '10000000-0000-0000-0000-000000000005', 'absolutely'),
                    ('20000000-0000-0000-0000-000000000401', N'Not at all', 1, '10000000-0000-0000-0000-000000000006', 'not-at-all'),
                    ('20000000-0000-0000-0000-000000000402', N'No', 2, '10000000-0000-0000-0000-000000000006', 'no'),
                    ('20000000-0000-0000-0000-000000000403', N'Neutral', 3, '10000000-0000-0000-0000-000000000006', 'neutral'),
                    ('20000000-0000-0000-0000-000000000404', N'Yes, mostly', 4, '10000000-0000-0000-0000-000000000006', 'yes-mostly'),
                    ('20000000-0000-0000-0000-000000000405', N'Absolutely', 5, '10000000-0000-0000-0000-000000000006', 'absolutely'),
                    ('20000000-0000-0000-0000-000000000501', N'😊 1 = Extremely likely', 1, '10000000-0000-0000-0000-000000000007', '1'),
                    ('20000000-0000-0000-0000-000000000502', N'😃 2 = Very likely', 2, '10000000-0000-0000-0000-000000000007', '2'),
                    ('20000000-0000-0000-0000-000000000503', N'😐 3 = Neutral', 3, '10000000-0000-0000-0000-000000000007', '3'),
                    ('20000000-0000-0000-0000-000000000504', N'😞 4 = Slightly likely', 4, '10000000-0000-0000-0000-000000000007', '4'),
                    ('20000000-0000-0000-0000-000000000505', N'😡 5 = Not at all likely', 5, '10000000-0000-0000-0000-000000000007', '5'),
                    ('20000000-0000-0000-0000-000000000601', N'Yes', 1, '10000000-0000-0000-0000-000000000008', 'yes'),
                    ('20000000-0000-0000-0000-000000000602', N'No', 2, '10000000-0000-0000-0000-000000000008', 'no');
                """);

            migrationBuilder.Sql(
                """
                UPDATE [Staff]
                SET [Name] = 'Neo Ramohabi'
                WHERE [Id] = '11111111-1111-1111-1111-111111111111';
                """);

            migrationBuilder.Sql(
                """
                UPDATE [Staff]
                SET [Name] = 'Tsepo Chefa'
                WHERE [Id] = '33333333-3333-3333-3333-333333333333';
                """);

            migrationBuilder.Sql(
                """
                UPDATE [Staff]
                SET [Name] = 'Mpho Phalafang'
                WHERE [Id] = '44444444-4444-4444-4444-444444444444';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [Staff]
                SET [Name] = 'Neo Ramohlabi'
                WHERE [Id] = '11111111-1111-1111-1111-111111111111';
                """);

            migrationBuilder.Sql(
                """
                UPDATE [Staff]
                SET [Name] = 'Ts''epo Chefa'
                WHERE [Id] = '33333333-3333-3333-3333-333333333333';
                """);

            migrationBuilder.Sql(
                """
                UPDATE [Staff]
                SET [Name] = 'Mpho Phahlang'
                WHERE [Id] = '44444444-4444-4444-4444-444444444444';
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM [Options]
                WHERE [Id] IN (
                    '20000000-0000-0000-0000-000000000001',
                    '20000000-0000-0000-0000-000000000002',
                    '20000000-0000-0000-0000-000000000003',
                    '20000000-0000-0000-0000-000000000004',
                    '20000000-0000-0000-0000-000000000101',
                    '20000000-0000-0000-0000-000000000102',
                    '20000000-0000-0000-0000-000000000103',
                    '20000000-0000-0000-0000-000000000104',
                    '20000000-0000-0000-0000-000000000105',
                    '20000000-0000-0000-0000-000000000106',
                    '20000000-0000-0000-0000-000000000201',
                    '20000000-0000-0000-0000-000000000202',
                    '20000000-0000-0000-0000-000000000203',
                    '20000000-0000-0000-0000-000000000204',
                    '20000000-0000-0000-0000-000000000205',
                    '20000000-0000-0000-0000-000000000301',
                    '20000000-0000-0000-0000-000000000302',
                    '20000000-0000-0000-0000-000000000303',
                    '20000000-0000-0000-0000-000000000304',
                    '20000000-0000-0000-0000-000000000305',
                    '20000000-0000-0000-0000-000000000401',
                    '20000000-0000-0000-0000-000000000402',
                    '20000000-0000-0000-0000-000000000403',
                    '20000000-0000-0000-0000-000000000404',
                    '20000000-0000-0000-0000-000000000405',
                    '20000000-0000-0000-0000-000000000501',
                    '20000000-0000-0000-0000-000000000502',
                    '20000000-0000-0000-0000-000000000503',
                    '20000000-0000-0000-0000-000000000504',
                    '20000000-0000-0000-0000-000000000505',
                    '20000000-0000-0000-0000-000000000601',
                    '20000000-0000-0000-0000-000000000602'
                );
                """);

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

            migrationBuilder.Sql(
                """
                DELETE FROM [Sections]
                WHERE [Id] IN (
                    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
                    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbc',
                    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbd',
                    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbe',
                    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbf'
                );
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO [Sections] ([Id], [Columns], [Order], [SurveyId], [Title])
                VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 1, 1, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', N'Main');
                """);

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
