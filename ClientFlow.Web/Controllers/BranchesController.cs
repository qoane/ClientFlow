using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClientFlow.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ClientFlow.Web.Controllers;

/// <summary>
/// Provides CRUD operations for branches.  Admins and SuperAdmins can create, update and
/// delete branches.  BranchAdmins can view only their own branch.  Branch definitions
/// include a name, report recipients and report time for daily reporting.  Kiosk
/// feedback and staff are linked to a branch via the BranchId foreign key.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BranchesController : ControllerBase
{
    private readonly AppDbContext _db;
    public BranchesController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lists all branches.  BranchAdmins are restricted to their assigned branch.  Admins and
    /// SuperAdmins can see all branches.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var branchIdClaim = User.FindFirstValue("BranchId");
        IQueryable<ClientFlow.Domain.Branches.Branch> query = _db.Branches.AsNoTracking();
        if (userRole == ClientFlow.Domain.Users.UserRole.BranchAdmin.ToString() && Guid.TryParse(branchIdClaim, out var bId))
        {
            query = query.Where(b => b.Id == bId);
        }
        var list = await query.Select(b => new { b.Id, b.Name, b.ReportRecipients, b.ReportTime }).ToListAsync(ct);
        return Ok(list);
    }

    /// <summary>
    /// Gets a single branch by ID.  BranchAdmins may only access their own branch.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var branchIdClaim = User.FindFirstValue("BranchId");
        if (userRole == ClientFlow.Domain.Users.UserRole.BranchAdmin.ToString() && branchIdClaim != id.ToString())
        {
            return Forbid();
        }
        var branch = await _db.Branches.FindAsync(new object?[] { id }, cancellationToken: ct);
        if (branch == null) return NotFound();
        return Ok(new { branch.Id, branch.Name, branch.ReportRecipients, branch.ReportTime });
    }

    /// <summary>
    /// Request body for creating or updating a branch.
    /// </summary>
    public record BranchReq(string Name, string? ReportRecipients, string? ReportTime);

    /// <summary>
    /// Creates a new branch.  Only Admins and SuperAdmins may create branches.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Create([FromBody] BranchReq req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required.");
        if (await _db.Branches.AnyAsync(b => b.Name == req.Name, ct))
            return Conflict("A branch with that name already exists.");
        var branch = new ClientFlow.Domain.Branches.Branch
        {
            Id = Guid.NewGuid(),
            Name = req.Name.Trim(),
            ReportRecipients = req.ReportRecipients?.Trim(),
            ReportTime = string.IsNullOrWhiteSpace(req.ReportTime) ? null : req.ReportTime.Trim()
        };
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync(ct);
        return Created($"/api/branches/{branch.Id}", new { branch.Id, branch.Name, branch.ReportRecipients, branch.ReportTime });
    }

    /// <summary>
    /// Updates an existing branch.  Only Admins and SuperAdmins may update branches.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] BranchReq req, CancellationToken ct)
    {
        var branch = await _db.Branches.FindAsync(new object?[] { id }, cancellationToken: ct);
        if (branch == null) return NotFound();
        var role = User.FindFirstValue(ClaimTypes.Role);
        var branchClaim = User.FindFirstValue("BranchId");
        // BranchAdmins can only update their own branch and cannot change the branch name
        if (role == ClientFlow.Domain.Users.UserRole.BranchAdmin.ToString())
        {
            if (branchClaim != id.ToString())
            {
                return Forbid();
            }
            // BranchAdmins may only update report recipients and time
            branch.ReportRecipients = string.IsNullOrWhiteSpace(req.ReportRecipients) ? null : req.ReportRecipients.Trim();
            branch.ReportTime = string.IsNullOrWhiteSpace(req.ReportTime) ? null : req.ReportTime.Trim();
        }
        else
        {
            // Admins and SuperAdmins can update all properties
            if (!string.IsNullOrWhiteSpace(req.Name)) branch.Name = req.Name.Trim();
            branch.ReportRecipients = string.IsNullOrWhiteSpace(req.ReportRecipients) ? null : req.ReportRecipients.Trim();
            branch.ReportTime = string.IsNullOrWhiteSpace(req.ReportTime) ? null : req.ReportTime.Trim();
        }
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Deletes a branch.  Only SuperAdmins may delete branches.  Deleting a branch will fail
    /// if there are dependent records such as kiosk feedback.  Use caution.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var branch = await _db.Branches.FindAsync(new object?[] { id }, cancellationToken: ct);
        if (branch == null) return NotFound();
        _db.Branches.Remove(branch);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}