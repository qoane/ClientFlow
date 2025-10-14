using ClientFlow.Domain.Feedback;
using ClientFlow.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ClientFlow.Domain.Users;
using Microsoft.AspNetCore.Authorization;

namespace ClientFlow.Web.Controllers;

/// <summary>
/// API endpoints for managing staff members.  These endpoints
/// allow the kiosk and admin pages to fetch the master staff list,
/// retrieve the active roster and add, update or delete staff.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StaffController : ControllerBase
{
    private readonly AppDbContext _db;
    public StaffController(AppDbContext db) => _db = db;

    /// <summary>
    /// Returns the list of all staff.  Pass ?active=true to only return staff marked as active.
    /// BranchAdmins are restricted to staff within their assigned branch.  Admins and
    /// SuperAdmins can view all staff regardless of branch.  Global staff (BranchId is null)
    /// are always included.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Staff>>> GetAll(
        [FromQuery] bool? active,
        [FromQuery] Guid? branchId,
        [FromQuery] string? branchName,
        CancellationToken ct)
    {
        var query = _db.Staff.AsNoTracking();
        // If a specific branch is requested, filter to that branch and global staff
        if (branchId.HasValue)
        {
            query = query.Where(s => s.BranchId == branchId.Value || s.BranchId == null);
        }
        else if (!string.IsNullOrWhiteSpace(branchName))
        {
            // Translate branch name to id
            var b = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(x => x.Name == branchName, ct);
            if (b != null)
            {
                var bid = b.Id;
                query = query.Where(s => s.BranchId == bid || s.BranchId == null);
            }
        }
        // Restrict BranchAdmins to their own branch (plus any global staff).  Pull role/branch from claims.
        var role = User.FindFirstValue(ClaimTypes.Role);
        var branchClaim = User.FindFirstValue("BranchId");
        if (role == ClientFlow.Domain.Users.UserRole.BranchAdmin.ToString() && Guid.TryParse(branchClaim, out var bId))
        {
            query = query.Where(s => s.BranchId == bId || s.BranchId == null);
        }
        if (active == true) query = query.Where(s => s.IsActive);
        var list = await query.OrderBy(s => s.Name).ToListAsync(ct);
        return Ok(list);
    }

    public sealed class StaffCreateRequest
    {
        public string? Name { get; set; }
        public string? PhotoUrl { get; set; }
        public bool? IsActive { get; set; }
        public Guid? BranchId { get; set; }
    }

    /// <summary>
    /// Adds a new staff member.  Name is required.  PhotoUrl and
    /// IsActive are optional (default to true).  BranchAdmins may only
    /// create staff in their assigned branch.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Staff>> Create([FromBody] StaffCreateRequest dto, CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Name is required");

        var callerRole = User.FindFirstValue(ClaimTypes.Role);
        var branchClaim = User.FindFirstValue("BranchId");

        Guid? branchId = null;
        if (callerRole == UserRole.BranchAdmin.ToString() && Guid.TryParse(branchClaim, out var branchFromClaim))
        {
            branchId = branchFromClaim;
        }
        else if (dto.BranchId.HasValue && dto.BranchId.Value != Guid.Empty)
        {
            var requestedBranchId = dto.BranchId.Value;
            var exists = await _db.Branches.AsNoTracking().AnyAsync(b => b.Id == requestedBranchId, ct);
            if (!exists)
            {
                return BadRequest("BranchId is invalid.");
            }
            branchId = requestedBranchId;
        }

        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            PhotoUrl = string.IsNullOrWhiteSpace(dto.PhotoUrl) ? null : dto.PhotoUrl.Trim(),
            IsActive = dto.IsActive ?? true,
            BranchId = branchId
        };

        _db.Staff.Add(staff);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetAll), new { id = staff.Id }, staff);
    }

    public sealed class StaffUpdateRequest
    {
        public string? Name { get; set; }
        public string? PhotoUrl { get; set; }
        public bool? IsActive { get; set; }
        public Guid? BranchId { get; set; }
    }

    /// <summary>
    /// Updates an existing staff member.  Only supplied fields are
    /// changed; unspecified properties remain untouched.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] StaffUpdateRequest dto, CancellationToken ct)
    {
        if (dto is null) return BadRequest("Body required.");

        var staff = await _db.Staff.FindAsync(new object?[] { id }, ct);
        if (staff == null) return NotFound();

        var callerRole = User.FindFirstValue(ClaimTypes.Role);
        var branchClaim = User.FindFirstValue("BranchId");
        if (callerRole == UserRole.BranchAdmin.ToString() && Guid.TryParse(branchClaim, out var bId))
        {
            if (staff.BranchId != bId)
            {
                return Forbid();
            }
        }

        if (dto.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Name cannot be empty.");
            staff.Name = dto.Name.Trim();
        }

        if (dto.PhotoUrl is not null)
        {
            staff.PhotoUrl = string.IsNullOrWhiteSpace(dto.PhotoUrl) ? null : dto.PhotoUrl.Trim();
        }

        if (dto.IsActive.HasValue)
        {
            staff.IsActive = dto.IsActive.Value;
        }

        if (callerRole != UserRole.BranchAdmin.ToString() && dto.BranchId is not null)
        {
            if (dto.BranchId == Guid.Empty)
            {
                staff.BranchId = null;
            }
            else
            {
                var exists = await _db.Branches.AsNoTracking().AnyAsync(b => b.Id == dto.BranchId, ct);
                if (!exists)
                {
                    return BadRequest("BranchId is invalid.");
                }
                staff.BranchId = dto.BranchId;
            }
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Deletes a staff member.  If kiosk feedback rows reference this
    /// staff member the caller must confirm cascading deletion via the
    /// cascade query parameter.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] bool cascade = false, CancellationToken ct)
    {
        var staff = await _db.Staff.FindAsync(new object?[] { id }, ct);
        if (staff == null) return NotFound();

        var callerRole = User.FindFirstValue(ClaimTypes.Role);
        var branchClaim = User.FindFirstValue("BranchId");
        if (callerRole == UserRole.BranchAdmin.ToString() && Guid.TryParse(branchClaim, out var bId))
        {
            if (staff.BranchId != bId)
            {
                return Forbid();
            }
        }

        var dependentCount = await _db.KioskFeedback.AsNoTracking().CountAsync(k => k.StaffId == id, ct);
        if (dependentCount > 0 && !cascade)
        {
            return Conflict(new
            {
                message = "Deleting this staff member will also remove related kiosk feedback. Resubmit with cascade=true to confirm.",
                dependentCount
            });
        }

        _db.Staff.Remove(staff);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Resets the staff table back to the seeded default values.
    /// Deletes all staff and inserts the seeded list from OnModelCreating.
    /// </summary>
    [HttpPost("reset")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        // Remove all
        var all = await _db.Staff.ToListAsync(ct);
        _db.Staff.RemoveRange(all);
        // Insert defaults (matching the seeding in AppDbContext)
        var defaults = new[]
        {
            new Staff { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Neo Ramohlabi", PhotoUrl = null, IsActive = true },
            new Staff { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Baradi Boikanyo", PhotoUrl = null, IsActive = true },
            new Staff { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Ts'epo Chefa", PhotoUrl = null, IsActive = true },
            new Staff { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Name = "Mpho Phahlang", PhotoUrl = null, IsActive = true }
        };
        await _db.Staff.AddRangeAsync(defaults, ct);
        await _db.SaveChangesAsync(ct);
        return Ok(defaults);
    }
}