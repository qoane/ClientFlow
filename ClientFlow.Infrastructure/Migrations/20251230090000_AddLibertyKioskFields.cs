using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientFlow.Infrastructure.Migrations
{
    public partial class AddLibertyKioskFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO [Questions] ([Id], [Key], [Order], [Prompt], [Required], [SectionId], [SettingsJson], [SurveyId], [Type])
                VALUES
                    ('10000000-0000-0000-0000-00000000000B', 'visit_reason', 11, N'What brings you in today?', 0, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbc', NULL, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'single'),
                    ('10000000-0000-0000-0000-00000000000C', 'visit_other', 12, N'If something else, please tell us.', 0, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbc', N'{"placeholder":"Short description"}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'text'),
                    ('10000000-0000-0000-0000-00000000000D', 'resolved_today', 13, N'Was your issue resolved today?', 0, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbc', N'{"yesLabel":"Yes, resolved","noLabel":"Not yet"}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'boolean'),
                    ('10000000-0000-0000-0000-00000000000E', 'service_rating', 14, N'How would you rate the service you received?', 0, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbc', N'{"stars":5}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'rating_stars'),
                    ('10000000-0000-0000-0000-00000000000F', 'services_used', 15, N'Which services did you use today?', 0, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbc', NULL, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'multi'),
                    ('10000000-0000-0000-0000-000000000010', 'recommend_score', 16, N'How likely are you to recommend Liberty to a friend?', 0, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbe', N'{"min":0,"max":10}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'nps_0_10'),
                    ('10000000-0000-0000-0000-000000000011', 'additional_feedback', 17, N'Any additional feedback for our team?', 0, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbe', N'{"placeholder":"Share anything that would help us improve.","rows":4}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'textarea');
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO [Options] ([Id], [Label], [Order], [QuestionId], [Value])
                VALUES
                    ('20000000-0000-0000-0000-000000000701', N'Open a new account', 1, '10000000-0000-0000-0000-00000000000B', 'new_account'),
                    ('20000000-0000-0000-0000-000000000702', N'Loan or mortgage', 2, '10000000-0000-0000-0000-00000000000B', 'loan'),
                    ('20000000-0000-0000-0000-000000000703', N'Account support', 3, '10000000-0000-0000-0000-00000000000B', 'support'),
                    ('20000000-0000-0000-0000-000000000704', N'Something else', 4, '10000000-0000-0000-0000-00000000000B', 'other'),
                    ('20000000-0000-0000-0000-000000000705', N'Teller window', 1, '10000000-0000-0000-0000-00000000000F', 'teller'),
                    ('20000000-0000-0000-0000-000000000706', N'Financial advisor', 2, '10000000-0000-0000-0000-00000000000F', 'advisor'),
                    ('20000000-0000-0000-0000-000000000707', N'Loan consultation', 3, '10000000-0000-0000-0000-00000000000F', 'loan'),
                    ('20000000-0000-0000-0000-000000000708', N'ATM or kiosk', 4, '10000000-0000-0000-0000-00000000000F', 'atm');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM [Options]
                WHERE [Id] IN (
                    '20000000-0000-0000-0000-000000000701',
                    '20000000-0000-0000-0000-000000000702',
                    '20000000-0000-0000-0000-000000000703',
                    '20000000-0000-0000-0000-000000000704',
                    '20000000-0000-0000-0000-000000000705',
                    '20000000-0000-0000-0000-000000000706',
                    '20000000-0000-0000-0000-000000000707',
                    '20000000-0000-0000-0000-000000000708'
                );
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM [Questions]
                WHERE [Id] IN (
                    '10000000-0000-0000-0000-00000000000B',
                    '10000000-0000-0000-0000-00000000000C',
                    '10000000-0000-0000-0000-00000000000D',
                    '10000000-0000-0000-0000-00000000000E',
                    '10000000-0000-0000-0000-00000000000F',
                    '10000000-0000-0000-0000-000000000010',
                    '10000000-0000-0000-0000-000000000011'
                );
                """);
        }
    }
}
