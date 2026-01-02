using System.Threading;
using ClientFlow.Application.Services;
using ClientFlow.Infrastructure;
using ClientFlow.Infrastructure.Repositories;
using ClientFlow.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ClientFlow.Infrastructure.Tests;

public class KioskSurveySubmissionTests
{
    [Fact]
    public async Task SubmitSurvey_Persists_Extended_Liberty_Answers()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        var controller = BuildController(context);

        var request = new SurveysController.SubmitSurveyRequest(
            new Dictionary<string, string?>
            {
                ["phone"] = "12345678",
                ["staff"] = "11111111-1111-1111-1111-111111111111",
                ["service"] = "support",
                ["satisfaction"] = "5",
                ["timeliness"] = "4",
                ["professionalism"] = "5",
                ["recommend"] = "4",
                ["follow_up"] = "no"
            },
            new SurveysController.SubmitSurveyAdditionalData(
                "Liberty",
                "liberty-kiosk-v2",
                new Dictionary<string, string?>
                {
                    ["visit_reason"] = "loan",
                    ["visit_other"] = "",
                    ["resolved_today"] = "true",
                    ["service_rating"] = "5",
                    ["services_used"] = "teller,advisor",
                    ["recommend_score"] = "9",
                    ["additional_feedback"] = "Great experience"
                }));

        var result = await controller.SubmitSurvey("liberty-nps", request, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);

        var answers = await context.Answers
            .Include(a => a.Question)
            .ToListAsync();
        var byKey = answers.ToDictionary(a => a.Question.Key, a => a);

        Assert.Equal("loan", byKey["visit_reason"].ValueText);
        Assert.Equal("true", byKey["resolved_today"].ValueText);
        Assert.Equal("teller,advisor", byKey["services_used"].ValueText);
        Assert.Equal("Great experience", byKey["additional_feedback"].ValueText);
        Assert.Equal(5m, byKey["service_rating"].ValueNumber);
        Assert.Equal(9m, byKey["recommend_score"].ValueNumber);
    }

    [Fact]
    public async Task SubmitSurvey_Allows_Legacy_Kiosk_Submission_Without_Extended_Fields()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        var controller = BuildController(context);

        var request = new SurveysController.SubmitSurveyRequest(
            new Dictionary<string, string?>
            {
                ["phone"] = "87654321",
                ["staff"] = "11111111-1111-1111-1111-111111111111",
                ["service"] = "support",
                ["satisfaction"] = "4",
                ["timeliness"] = "3",
                ["professionalism"] = "4",
                ["recommend"] = "3",
                ["follow_up"] = "yes"
            },
            null);

        var result = await controller.SubmitSurvey("liberty-nps", request, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);

        var keys = await context.Answers
            .Include(a => a.Question)
            .Select(a => a.Question.Key)
            .ToListAsync();

        Assert.DoesNotContain("visit_reason", keys);
        Assert.DoesNotContain("recommend_score", keys);
        Assert.DoesNotContain("additional_feedback", keys);
    }

    private static SurveysController BuildController(AppDbContext context)
    {
        var surveyRepo = new SurveyRepository(context);
        var responseRepo = new ResponseRepository(context);
        var optionRepo = new OptionRepository(context);
        var ruleRepo = new RuleRepository(context);
        var versionRepo = new SurveyVersionRepository(context);
        var uow = new UnitOfWork(context);
        var service = new SurveyService(surveyRepo, responseRepo, optionRepo, ruleRepo, versionRepo, uow);

        return new SurveysController(service, surveyRepo, responseRepo, uow, context);
    }
}
