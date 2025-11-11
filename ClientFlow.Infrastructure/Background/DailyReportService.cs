using ClientFlow.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientFlow.Infrastructure.Background;

/// <summary>
/// A hosted service that sends a daily feedback summary email to the
/// recipients specified in the Settings table.  The send time is
/// configured via the "ReportTime" setting in HH:mm format (local
/// system timezone).  Reports include statistics such as total
/// responses, average ratings, average duration and counts per staff
/// and branch.
/// </summary>
public class DailyReportService : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyReportService> _logger;
    private Timer? _timer;
    public DailyReportService(IServiceScopeFactory scopeFactory, ILogger<DailyReportService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DailyReportService starting");
        ScheduleNext();
        return Task.CompletedTask;
    }

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    private void ScheduleNext()
    {
        TimeSpan delay;
        var now = DateTime.Now;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Determine the earliest next run time across all branches. Each branch can specify
            // its own ReportTime in HH:mm format. If none are configured a default of 08:00 is
            // used. We calculate the next occurrence of each branch's scheduled time and then
            // select the minimum. The service is scheduled to run at that time and will send
            // reports for all branches.
            var runTimes = new List<DateTime>();
            var branchTimes = db.Branches.AsNoTracking().Select(b => b.ReportTime).ToList();

            if (branchTimes.Count == 0)
            {
                branchTimes.Add("08:00");
            }

            foreach (var timeStr in branchTimes)
            {
                var tStr = string.IsNullOrWhiteSpace(timeStr) ? "08:00" : timeStr!;
                var parts = tStr.Split(':', StringSplitOptions.RemoveEmptyEntries);
                int hour = 8, minute = 0;
                if (parts.Length >= 2)
                {
                    int.TryParse(parts[0], out hour);
                    int.TryParse(parts[1], out minute);
                }

                var candidate = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
                if (candidate <= now)
                {
                    candidate = candidate.AddDays(1);
                }

                runTimes.Add(candidate);
            }

            var nextRun = runTimes.Min();
            delay = nextRun - now;
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            _logger.LogInformation("Daily report scheduled at {NextRun}", nextRun);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule next daily report. Retrying in {RetryDelay}.", RetryDelay);
            delay = RetryDelay;
        }

        _timer?.Dispose();
        _timer = new Timer(async _ =>
        {
            try
            {
                await SendReportAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending daily report");
            }
            finally
            {
                ScheduleNext();
            }
        }, null, delay, Timeout.InfiniteTimeSpan);
    }

    private async Task SendReportAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
        // Loop through each branch and send its own report if recipients are defined.  If no
        // recipients are configured for a branch then skip that branch.  Reports are sent for
        // feedback collected in the last 24 hours (UTC) for the specific branch.
        var since = DateTimeOffset.UtcNow.AddDays(-1);
        var branches = await db.Branches.AsNoTracking().ToListAsync();
        foreach (var branch in branches)
        {
            if (string.IsNullOrWhiteSpace(branch.ReportRecipients))
            {
                _logger.LogInformation("No report recipients configured for branch {Branch}; skipping", branch.Name);
                continue;
            }
            var recipients = branch.ReportRecipients.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var list = await db.KioskFeedback
                .Include(x => x.Staff)
                .Where(x => x.BranchId == branch.Id && x.CreatedUtc >= since)
                .ToListAsync();
            if (list.Count == 0)
            {
                _logger.LogInformation("No feedback to report in last 24 hours for branch {Branch}", branch.Name);
                continue;
            }
            double avg(List<int> values) => values.Count == 0 ? 0 : values.Average();
            var times = list.Select(x => x.TimeRating).ToList();
            var respects = list.Select(x => x.RespectRating).ToList();
            var overalls = list.Select(x => x.OverallRating).ToList();
            var durations = list.Select(x => x.DurationSeconds).ToList();
            var avgTime = avg(times);
            var avgRespect = avg(respects);
            var avgOverall = avg(overalls);
            var avgDuration = durations.Count == 0 ? 0 : durations.Average();
            var staffCounts = list.GroupBy(x => x.Staff.Name).Select(g => new { Name = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).ToList();
            // Build HTML summary for this branch
            var sb = new StringBuilder();
            sb.AppendLine($"<h2>Daily Feedback Summary — {System.Net.WebUtility.HtmlEncode(branch.Name)}</h2>");
            sb.AppendLine($"<p>Total responses: {list.Count}</p>");
            sb.AppendLine("<ul>");
            sb.AppendLine($"<li>Average Time Rating: {avgTime:F2}</li>");
            sb.AppendLine($"<li>Average Respect Rating: {avgRespect:F2}</li>");
            sb.AppendLine($"<li>Average Overall Rating: {avgOverall:F2}</li>");
            sb.AppendLine($"<li>Average Duration: {avgDuration:F2} seconds</li>");
            sb.AppendLine("</ul>");
            sb.AppendLine("<h3>Responses by Staff</h3><table border='1' cellpadding='4' cellspacing='0'><tr><th>Staff</th><th>Count</th></tr>");
            foreach (var sc in staffCounts)
                sb.AppendLine($"<tr><td>{System.Net.WebUtility.HtmlEncode(sc.Name)}</td><td>{sc.Count}</td></tr>");
            sb.AppendLine("</table>");
            var subject = $"Daily Feedback Report ({DateTime.Now:yyyy-MM-dd}) — {branch.Name}";
            await emailService.SendAsync(recipients, subject, sb.ToString());
            _logger.LogInformation("Daily report for branch {Branch} sent to {Recipients}", branch.Name, string.Join(", ", recipients));
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DailyReportService stopping");
        _timer?.Dispose();
        return Task.CompletedTask;
    }
    public void Dispose()
    {
        _timer?.Dispose();
    }
}