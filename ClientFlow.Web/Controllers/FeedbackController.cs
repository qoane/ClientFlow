// ClientFlow.Web/Controllers/FeedbackController.cs
using ClientFlow.Domain.Feedback;
using ClientFlow.Application.DTOs;
using ClientFlow.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ClientFlow.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
// Require authentication for all endpoints except the anonymous feedback submission (POST)
[Authorize]
public class FeedbackController : ControllerBase
{
    private readonly AppDbContext _db;
    public FeedbackController(AppDbContext db) => _db = db;

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Post([FromBody] FeedbackDto dto, CancellationToken ct)
    {
        if (dto is null) return BadRequest("Body required.");

        if (string.IsNullOrWhiteSpace(dto.Phone))
            return BadRequest("Phone number is required.");
        var phoneValue = NormalizePhone(dto.Phone);
        if (!IsValidLocalPhone(phoneValue))
            return BadRequest("Please enter an 8 digit local phone number.");

        if ((dto.TimeRating is not null && dto.TimeRating is < 1 or > 5) ||
            (dto.RespectRating is not null && dto.RespectRating is < 1 or > 5) ||
            (dto.OverallRating is not null && dto.OverallRating is < 1 or > 5) ||
            (dto.RecommendRating is not null && dto.RecommendRating is < 1 or > 5))
            return BadRequest("Ratings must be between 1 and 5.");

        // Resolve staff: prefer ID, else by name (create if needed)
        Guid? staffId = dto.StaffId;

        if (staffId.HasValue)
        {
            bool exists = await _db.Staff.AsNoTracking().AnyAsync(s => s.Id == staffId.Value, ct);
            if (!exists) staffId = null; // fall back to name resolution
        }

        if (!staffId.HasValue)
        {
            if (string.IsNullOrWhiteSpace(dto.StaffName))
                return BadRequest("StaffId not found. Provide StaffName to create or resolve a staff member.");

            var existing = await _db.Staff.FirstOrDefaultAsync(s => s.Name == dto.StaffName, ct);
            if (existing is null)
            {
                existing = new ClientFlow.Domain.Feedback.Staff
                {
                    Id = Guid.NewGuid(),
                    Name = dto.StaffName!,
                    PhotoUrl = null,
                    IsActive = true
                };
                _db.Staff.Add(existing);
                await _db.SaveChangesAsync(ct);
            }
            staffId = existing.Id;
        }

        // Determine branch: prefer BranchId if supplied, else BranchName.  Ensure a valid branch exists.
        Guid selectedBranchId;
        if (dto.BranchId.HasValue)
        {
            selectedBranchId = dto.BranchId.Value;
            bool exists = await _db.Branches.AsNoTracking().AnyAsync(b => b.Id == selectedBranchId, ct);
            if (!exists)
                return BadRequest("Invalid BranchId");
        }
        else if (!string.IsNullOrWhiteSpace(dto.BranchName))
        {
            var b = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(x => x.Name == dto.BranchName, ct);
            if (b is null)
                return BadRequest("Branch name not found");
            selectedBranchId = b.Id;
        }
        else
        {
            // Neither BranchId nor BranchName was provided.  Fallback to a default branch
            // if one exists.  This prevents the kiosk from failing if it cannot
            // determine its branch (e.g. Settings endpoint is unreachable).  Use the
            // first branch ordered by name as a reasonable default.
            var defaultBranch = await _db.Branches.AsNoTracking().OrderBy(b => b.Name).FirstOrDefaultAsync(ct);
            if (defaultBranch is null)
            {
                return BadRequest("BranchId or BranchName is required");
            }
            selectedBranchId = defaultBranch.Id;
        }
        // Determine branch name for legacy column; look up once to avoid duplicate queries
        string? branchNameVal = null;
        try
        {
            var br = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == selectedBranchId, ct);
            branchNameVal = br?.Name;
        }
        catch { }
        var entity = new ClientFlow.Domain.Feedback.KioskFeedback
        {
            Id = Guid.NewGuid(),
            StaffId = staffId.Value,
            TimeRating = dto.TimeRating ?? 0,
            RespectRating = dto.RespectRating ?? 0,
            OverallRating = dto.OverallRating ?? 0,
            RecommendRating = dto.RecommendRating,
            Phone = string.IsNullOrWhiteSpace(phoneValue) ? null : phoneValue,
            ServiceType = string.IsNullOrWhiteSpace(dto.ServiceType) ? null : dto.ServiceType.Trim(),
            Gender = string.IsNullOrWhiteSpace(dto.Gender) ? null : dto.Gender.Trim(),
            AgeRange = string.IsNullOrWhiteSpace(dto.AgeRange) ? null : dto.AgeRange.Trim(),
            City = string.IsNullOrWhiteSpace(dto.City) ? null : dto.City.Trim(),
            PoliciesJson = dto.Policies is { Count: > 0 }
                ? JsonSerializer.Serialize(dto.Policies.Where(p => !string.IsNullOrWhiteSpace(p)))
                : null,
            ContactPreference = string.IsNullOrWhiteSpace(dto.ContactPreference) ? null : dto.ContactPreference.Trim(),
            Comment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim(),
            StartedUtc = dto.StartedUtc ?? DateTimeOffset.UtcNow,
            DurationSeconds = dto.DurationSeconds ?? 0,
            BranchId = selectedBranchId,
            BranchName = branchNameVal,
            CreatedUtc = DateTimeOffset.UtcNow
        };

        _db.KioskFeedback.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = entity.Id }, new { entity.Id });
    }

    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("266", StringComparison.Ordinal) && digits.Length >= 11)
        {
            return digits[^8..];
        }

        return digits;
    }

    private static bool IsValidLocalPhone(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length == 8
        && value.All(char.IsDigit);

    // NOTE:
    // The unfiltered GET method previously returned all kiosk feedback without any query parameters.
    // It duplicated the signature of the more flexible List method below and caused Swagger to
    // report a conflict because both actions shared the same HTTP method and route.  The
    // parameterised List endpoint defined later handles both the unfiltered and filtered cases
    // (all its parameters are optional), so the duplicate has been removed.

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClientFlow.Domain.Feedback.KioskFeedback>> Get(Guid id, CancellationToken ct)
    {
        var item = await _db.KioskFeedback.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    // NOTE:
    // The simple summary endpoint without query parameters has been removed because it conflicted
    // with the richer summary endpoint defined later.  The enhanced Summary method accepts
    // optional filtering parameters and returns all of the aggregated data, so callers who do
    // not require filters can still invoke /api/feedback/summary without query parameters.

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ClientFlow.Domain.Feedback.KioskFeedback>>> List(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? branchName = null,
        Guid? branchId = null,
        Guid? staffId = null,
        int? minOverall = null,
        int? minRespect = null,
        int? minTime = null,
        int? minDuration = null,
        int? maxDuration = null,
        string? q = null,
        CancellationToken ct = default)
    {
        var query = _db.KioskFeedback.AsNoTracking().AsQueryable();

        // Restrict BranchAdmins to their own branch.  Pull role/branch from claims.
        var role = User.FindFirstValue(ClaimTypes.Role);
        var branchClaim = User.FindFirstValue("BranchId");
        if (role == ClientFlow.Domain.Users.UserRole.BranchAdmin.ToString() && Guid.TryParse(branchClaim, out var callerBranchId))
        {
            // Force the branch filter to the caller's branch regardless of query parameters
            query = query.Where(x => x.BranchId == callerBranchId);
        }

        if (from is not null) query = query.Where(x => x.CreatedUtc >= from);
        if (to is not null) query = query.Where(x => x.CreatedUtc < to);
        if (branchId is not null) query = query.Where(x => x.BranchId == branchId);
        else if (!string.IsNullOrWhiteSpace(branchName)) query = query.Where(x => x.BranchName == branchName);
        if (staffId is not null) query = query.Where(x => x.StaffId == staffId);
        if (minOverall is not null) query = query.Where(x => x.OverallRating >= minOverall);
        if (minRespect is not null) query = query.Where(x => x.RespectRating >= minRespect);
        if (minTime is not null) query = query.Where(x => x.TimeRating >= minTime);
        if (minDuration is not null) query = query.Where(x => x.DurationSeconds >= minDuration);
        if (maxDuration is not null) query = query.Where(x => x.DurationSeconds <= maxDuration);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var qs = q.Trim();
            query = query.Where(x =>
                (x.Phone ?? "").Contains(qs) ||
                (x.BranchName ?? "").Contains(qs));
        }

        var items = await query.OrderByDescending(x => x.CreatedUtc).ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<FeedbackSummary>> Summary(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? branchName = null,
        Guid? branchId = null,
        Guid? staffId = null,
        int? minOverall = null,
        int? minRespect = null,
        int? minTime = null,
        int? minDuration = null,
        int? maxDuration = null,
        string? q = null,
        CancellationToken ct = default)
    {
        var query = _db.KioskFeedback.AsNoTracking().AsQueryable();

        // Restrict BranchAdmins to their own branch.  Pull role/branch from claims.
        var role = User.FindFirstValue(ClaimTypes.Role);
        var branchClaim = User.FindFirstValue("BranchId");
        if (role == ClientFlow.Domain.Users.UserRole.BranchAdmin.ToString() && Guid.TryParse(branchClaim, out var callerBranchId))
        {
            query = query.Where(x => x.BranchId == callerBranchId);
        }

        if (from is not null) query = query.Where(x => x.CreatedUtc >= from);
        if (to is not null) query = query.Where(x => x.CreatedUtc < to);
        if (branchId is not null) query = query.Where(x => x.BranchId == branchId);
        else if (!string.IsNullOrWhiteSpace(branchName)) query = query.Where(x => x.BranchName == branchName);
        if (staffId is not null) query = query.Where(x => x.StaffId == staffId);
        if (minOverall is not null) query = query.Where(x => x.OverallRating >= minOverall);
        if (minRespect is not null) query = query.Where(x => x.RespectRating >= minRespect);
        if (minTime is not null) query = query.Where(x => x.TimeRating >= minTime);
        if (minDuration is not null) query = query.Where(x => x.DurationSeconds >= minDuration);
        if (maxDuration is not null) query = query.Where(x => x.DurationSeconds <= maxDuration);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(x =>
                (x.Phone ?? "").Contains(q) ||
                (x.BranchName ?? "").Contains(q));
        }

        var list = await query.ToListAsync(ct);
        var total = list.Count;

        double Avg(IEnumerable<int> xs) => xs.Any() ? xs.Average() : 0;

        var times = list.Select(x => x.TimeRating).ToList();
        var respects = list.Select(x => x.RespectRating).ToList();
        var overalls = list.Select(x => x.OverallRating).ToList();
        var durations = list.Select(x => x.DurationSeconds).ToList();

        // Time series (daily)
        var series = list
            .GroupBy(x => x.CreatedUtc.Date)
            .Select(g => new SeriesPoint(g.Key, g.Count(),
                g.Average(v => v.OverallRating)))
            .OrderBy(x => x.Date)
            .ToList();

        var model = new FeedbackSummary
        {
            Total = total,
            AvgTime = Avg(times),
            AvgRespect = Avg(respects),
            AvgOverall = Avg(overalls),
            AvgDuration = durations.Any() ? durations.Average() : 0,
            TimeCounts = Enumerable.Range(1, 5).Select(i => new RatingCount(i, times.Count(v => v == i))).ToList(),
            RespectCounts = Enumerable.Range(1, 5).Select(i => new RatingCount(i, respects.Count(v => v == i))).ToList(),
            OverallCounts = Enumerable.Range(1, 5).Select(i => new RatingCount(i, overalls.Count(v => v == i))).ToList(),
            DurationBuckets = new List<DurationBucket>
        {
            new("<30s",   durations.Count(d => d > 0  && d < 30)),
            new("30-60s", durations.Count(d => d >= 30 && d < 60)),
            new("60-90s", durations.Count(d => d >= 60 && d < 90)),
            new(">=90s",  durations.Count(d => d >= 90)),
        },
            StaffCounts = list.GroupBy(x => x.StaffId)
                .Select(g => new StaffCount(g.Key, g.Count()))
                .OrderByDescending(x => x.Count).ToList(),
            BranchCounts = list.GroupBy(x => string.IsNullOrWhiteSpace(x.BranchName) ? "Unknown" : x.BranchName!)
                .Select(g => new BranchCount(g.Key, g.Count()))
                .OrderByDescending(x => x.Count).ToList(),
            Series = series
        };

        return Ok(model);
    }

    /// <summary>
    /// Returns a flat list of respondents (phone numbers) for the given branch.
    /// Administrators can retrieve respondents for any branch by passing
    /// branchId as a query parameter.  BranchAdmins are restricted to their
    /// assigned branch regardless of the branchId parameter.  Only entries
    /// with a non-null Phone value are returned.  The result includes the
    /// phone number, branch name and creation timestamp for each response.
    /// </summary>
    [HttpGet("respondents")]
    [Authorize]
    public async Task<IActionResult> Respondents([FromQuery] Guid? branchId, CancellationToken ct = default)
    {
        var query = _db.KioskFeedback.AsNoTracking().Where(x => x.Phone != null);
        // Restrict BranchAdmins to their own branch regardless of the query
        var role = User.FindFirstValue(ClaimTypes.Role);
        var branchClaim = User.FindFirstValue("BranchId");
        if (role == ClientFlow.Domain.Users.UserRole.BranchAdmin.ToString() && Guid.TryParse(branchClaim, out var callerBranch))
        {
            query = query.Where(x => x.BranchId == callerBranch);
        }
        else if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }
        var list = await query
            .Select(x => new { x.Phone, x.BranchName, x.BranchId, x.CreatedUtc })
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync(ct);
        return Ok(list);
    }


    public sealed record SeriesPoint(DateTime Date, int Count, double AvgOverall);

    public sealed record RatingCount(int Rating, int Count);
    public sealed record StaffCount(Guid StaffId, int Count);
    public sealed record DurationBucket(string Label, int Count);
    public sealed record BranchCount(string Branch, int Count);

    public sealed class FeedbackSummary
    {
        public int Total { get; set; }
        public double AvgTime { get; set; }
        public double AvgRespect { get; set; }
        public double AvgOverall { get; set; }
        public double AvgDuration { get; set; }
        public List<RatingCount> TimeCounts { get; set; } = new();
        public List<RatingCount> RespectCounts { get; set; } = new();
        public List<RatingCount> OverallCounts { get; set; } = new();
        public List<DurationBucket> DurationBuckets { get; set; } = new();
        public List<StaffCount> StaffCounts { get; set; } = new();
        public List<BranchCount> BranchCounts { get; set; } = new();
        public List<SeriesPoint> Series { get; set; } = new();
    }

    /// <summary>
    /// Deletes feedback records.  Admins and SuperAdmins may delete all feedback or
    /// feedback for a specific branch.  BranchAdmins may only delete feedback for
    /// their own branch.  Passing a branchId query parameter restricts deletion
    /// to that branch.  Without a branchId, Admins and SuperAdmins delete all.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> Delete([FromQuery] Guid? branchId, CancellationToken ct)
    {
        var callerRole = User.FindFirstValue(ClaimTypes.Role);
        var branchClaim = User.FindFirstValue("BranchId");
        // BranchAdmin can only delete their own branch
        if (callerRole == ClientFlow.Domain.Users.UserRole.BranchAdmin.ToString())
        {
            if (!Guid.TryParse(branchClaim, out var callerBranchId))
            {
                return Forbid();
            }
            if (branchId.HasValue && branchId.Value != callerBranchId)
            {
                return Forbid();
            }
            // Force branchId to caller's branch
            branchId = callerBranchId;
        }
        // Only Admins or SuperAdmins can clear feedback without specifying a branch
        if (!branchId.HasValue && callerRole != ClientFlow.Domain.Users.UserRole.Admin.ToString() && callerRole != ClientFlow.Domain.Users.UserRole.SuperAdmin.ToString())
        {
            return Forbid();
        }
        var query = _db.KioskFeedback.AsQueryable();
        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }
        _db.KioskFeedback.RemoveRange(query);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
