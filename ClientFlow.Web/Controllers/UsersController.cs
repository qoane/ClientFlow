using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClientFlow.Application.Services;
using ClientFlow.Domain.Users;
using ClientFlow.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ClientFlow.Web.Controllers;

/// <summary>
/// Provides CRUD operations for administrative users and handles authentication via JWT.  Only
/// SuperAdmin users may create or delete users.  Admin users may view users but cannot create
/// SuperAdmins.  BranchAdmins are restricted to their own branch and cannot view or modify
/// other users.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public UsersController(AppDbContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    /// <summary>
    /// Request body for the login endpoint.
    /// </summary>
    public record LoginRequest(string Email, string Password);

    /// <summary>
    /// Authenticates a user and returns a JWT on success.  The caller must provide a valid
    /// email and password.  The response includes the token, the user's role and any
    /// associated branch identifier.  On failure a 401 response is returned.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email, ct);
        if (user == null || !_auth.VerifyPassword(req.Password, user.PasswordHash))
        {
            return Unauthorized();
        }
        var token = _auth.GenerateJwtToken(user);
        return Ok(new { token, role = user.Role.ToString(), userId = user.Id, branchId = user.BranchId });
    }

    /// <summary>
    /// Request body for creating a new user.  Only SuperAdmin users are authorised to
    /// register new users.  BranchAdmins cannot create users.  The password is hashed
    /// before being stored.
    /// </summary>
    // Using a record for strong typing when returning responses is still helpful, but we
    // will not rely on model binding for the Register method because System.Text.Json
    // can fail to convert string values for enums or handle varied casing.  Instead
    // we will manually parse the incoming JSON so that values like "Branch Admin" or
    // lowercase property names are accepted.  The record below is retained for
    // completeness but is not used by the Register action.
    public record RegisterRequest(string Email, string Password, UserRole Role, Guid? BranchId);

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Register([FromBody] System.Text.Json.JsonElement body, CancellationToken ct)
    {
        // Manually parse incoming JSON rather than relying on model binding so that
        // property names can be any casing and role strings can include spaces.
        string? email = null;
        string? password = null;
        string? roleStr = null;
        Guid? branchId = null;
        foreach (var prop in body.EnumerateObject())
        {
            var name = prop.Name.ToLowerInvariant();
            switch (name)
            {
                case "email":
                    email = prop.Value.GetString();
                    break;
                case "password":
                    password = prop.Value.GetString();
                    break;
                case "role":
                    roleStr = prop.Value.GetString();
                    break;
                case "branchid":
                    if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Null)
                    {
                        branchId = null;
                    }
                    else if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        // Accept GUID string, empty string => null
                        var raw = prop.Value.GetString();
                        if (string.IsNullOrWhiteSpace(raw))
                        {
                            branchId = null;
                        }
                        else if (Guid.TryParse(raw, out var gid))
                        {
                            branchId = gid;
                        }
                    }
                    else if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Undefined)
                    {
                        branchId = null;
                    }
                    else
                    {
                        // Attempt to read as GUID
                        try { branchId = prop.Value.GetGuid(); }
                        catch { branchId = null; }
                    }
                    break;
            }
        }
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return BadRequest("Email and Password are required.");
        }
        // Determine role; default to BranchAdmin if unspecified
        if (!Enum.TryParse<UserRole>(roleStr?.Replace(" ", ""), ignoreCase: true, out var role))
        {
            role = UserRole.BranchAdmin;
        }
        // Prevent non-super users from creating SuperAdmins (enforced by Authorize)
        if (role == UserRole.SuperAdmin && !User.IsInRole(UserRole.SuperAdmin.ToString()))
        {
            return Forbid();
        }
        // Ensure email is unique
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
        {
            return Conflict("Email already exists.");
        }
        // Validate branch assignment
        if (branchId.HasValue && !await _db.Branches.AnyAsync(b => b.Id == branchId.Value, ct))
        {
            return BadRequest("Invalid BranchId");
        }
        Guid? createdBy = null;
        var creatorClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(creatorClaim, out var creatorId))
        {
            createdBy = creatorId;
        }
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim(),
            PasswordHash = _auth.HashPassword(password),
            Role = role,
            BranchId = branchId,
            CreatedByUserId = createdBy
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return Created($"/api/users/{user.Id}", new { user.Id, user.Email, role = user.Role.ToString(), user.BranchId });
    }

    /// <summary>
    /// Lists all users in the system.  Only Admins and SuperAdmins may view the full list.
    /// BranchAdmins are restricted to their own user record.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var callerRole = User.FindFirstValue(ClaimTypes.Role);
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid.TryParse(callerId, out var callerGuid);
        // BranchAdmins can only see themselves
        IQueryable<User> query = _db.Users.AsNoTracking();
        if (callerRole == UserRole.BranchAdmin.ToString())
        {
            query = query.Where(u => u.Id == callerGuid);
        }
        var users = await query
            .Select(u => new { u.Id, u.Email, Role = u.Role.ToString(), u.BranchId })
            .ToListAsync(ct);
        return Ok(users);
    }

    /// <summary>
    /// Returns all users that were created by branch managers, grouped by the
    /// manager's branch.  Only SuperAdmin users may access this endpoint.
    /// </summary>
    [HttpGet("branch-manager-created")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> ListCreatedByManagers(CancellationToken ct)
    {
        var createdUsers = await _db.Users
            .AsNoTracking()
            .Where(u => u.CreatedByUserId != null)
            .Select(u => new
            {
                u.Id,
                u.Email,
                Role = u.Role.ToString(),
                u.BranchId,
                BranchName = u.Branch != null ? u.Branch.Name : null,
                u.CreatedByUserId,
                CreatedByEmail = u.CreatedByUser != null ? u.CreatedByUser.Email : null,
                ManagerBranchId = u.CreatedByUser != null ? u.CreatedByUser.BranchId : null,
                ManagerBranchName = u.CreatedByUser != null && u.CreatedByUser.Branch != null ? u.CreatedByUser.Branch.Name : null
            })
            .ToListAsync(ct);

        var grouped = createdUsers
            .GroupBy(x => new { x.ManagerBranchId, x.ManagerBranchName })
            .OrderBy(g => g.Key.ManagerBranchName ?? "Unassigned")
            .Select(g => new
            {
                branchId = g.Key.ManagerBranchId,
                branchName = g.Key.ManagerBranchName ?? "Unassigned",
                totalUsers = g.Count(),
                managers = g
                    .GroupBy(x => new { x.CreatedByUserId, x.CreatedByEmail })
                    .OrderBy(mg => mg.Key.CreatedByEmail ?? string.Empty)
                    .Select(mg => new
                    {
                        managerId = mg.Key.CreatedByUserId,
                        managerEmail = mg.Key.CreatedByEmail ?? "Unknown",
                        userCount = mg.Count(),
                        users = mg
                            .OrderBy(u => u.Email)
                            .Select(u => new
                            {
                                id = u.Id,
                                email = u.Email,
                                role = u.Role,
                                branchId = u.BranchId,
                                branchName = u.BranchName ?? "Unassigned"
                            })
                    })
            })
            .ToList();

        return Ok(grouped);
    }

    /// <summary>
    /// Retrieves a user by ID.  BranchAdmins may only access their own record.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var callerRole = User.FindFirstValue(ClaimTypes.Role);
        if (!Guid.TryParse(callerId, out var callerGuid))
        {
            return Unauthorized();
        }
        // BranchAdmin can only view their own record
        if (callerRole == UserRole.BranchAdmin.ToString() && callerGuid != id)
        {
            return Forbid();
        }
        var user = await _db.Users.FindAsync(new object?[] { id }, cancellationToken: ct);
        if (user == null)
            return NotFound();
        return Ok(new { user.Id, user.Email, Role = user.Role.ToString(), user.BranchId });
    }

    /// <summary>
    /// Deletes a user.  Only SuperAdmins may delete users.  SuperAdmins cannot delete themselves.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (callerId == id.ToString())
        {
            return BadRequest("You cannot delete yourself.");
        }
        var user = await _db.Users.FindAsync(new object?[] { id }, cancellationToken: ct);
        if (user == null)
            return NotFound();
        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}