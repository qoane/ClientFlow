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

    /// <summary>
    /// Adds a new staff member.  Name is required.  PhotoUrl and
    /// IsActive are optional (default to true).
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Staff>> Create([FromBody] Staff dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Name is required");
        // Determine branch assignment: BranchAdmins can only create staff in their own branch.
        var callerRole = User.FindFirstValue(ClaimTypes.Role);
        var branchClaim = User.FindFirstValue("BranchId");
        Guid? branchId = null;
        if (callerRole == UserRole.BranchAdmin.ToString() && Guid.TryParse(branchClaim, out var bId))
        {
            branchId = bId;
        }
        else
        {
            // For Admins and SuperAdmins we honour the BranchId supplied in the body (if any)
            if (dto.BranchId != Guid.Empty) branchId = dto.BranchId;
        }
        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            PhotoUrl = string.IsNullOrWhiteSpace(dto.PhotoUrl) ? null : dto.PhotoUrl.Trim(),
            IsActive = dto.IsActive,
            BranchId = branchId
        };
        _db.Staff.Add(staff);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetAll), new { id = staff.Id }, staff);
    }

    /// <summary>
    /// Updates an existing staff member.  Only the Name, PhotoUrl and
    /// IsActive properties may be updated.  Fields not supplied will
    /// remain unchanged.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] Staff dto, CancellationToken ct)
    {
        var staff = await _db.Staff.FindAsync(new object?[] { id }, ct);
        if (staff == null) return NotFound();
        // Restrict BranchAdmins to their own branch
        var callerRole = User.FindFirstValue(ClaimTypes.Role);
        var branchClaim = User.FindFirstValue("BranchId");
        if (callerRole == UserRole.BranchAdmin.ToString() && Guid.TryParse(branchClaim, out var bId))
        {
            if (staff.BranchId != bId)
            {
                return Forbid();
            }
        }
        if (!string.IsNullOrWhiteSpace(dto.Name)) staff.Name = dto.Name.Trim();
        if (!string.IsNullOrWhiteSpace(dto.PhotoUrl)) staff.PhotoUrl = dto.PhotoUrl.Trim();
        // If IsActive supplied in body we update it; otherwise leave unchanged
        staff.IsActive = dto.IsActive;
        // For Admins and SuperAdmins allow updating branch assignment
        if (callerRole != UserRole.BranchAdmin.ToString())
        {
            if (dto.BranchId != Guid.Empty) staff.BranchId = dto.BranchId;
        }
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Deletes a staff member.  Feedback rows referencing this staff
    /// member will be cascaded per the model configuration.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var staff = await _db.Staff.FindAsync(new object?[] { id }, ct);
        if (staff == null) return NotFound();
        // Restrict BranchAdmins to their own branch
        var callerRole = User.FindFirstValue(ClaimTypes.Role);
        var branchClaim = User.FindFirstValue("BranchId");
        if (callerRole == UserRole.BranchAdmin.ToString() && Guid.TryParse(branchClaim, out var bId))
        {
            if (staff.BranchId != bId)
            {
                return Forbid();
            }
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