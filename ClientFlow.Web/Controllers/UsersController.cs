using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClientFlow.Application.Services;
using ClientFlow.Domain.Users;
using ClientFlow.Infrastructure;
using ClientFlow.Infrastructure.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;

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
    private readonly EmailService _email;

    public UsersController(AppDbContext db, AuthService auth, EmailService email)
    {
        _db = db;
        _auth = auth;
        _email = email;
    }

    /// <summary>
    /// Request body for the login endpoint.
    /// </summary>
    public record LoginRequest(string Email, string Password);

    public record ChangePasswordRequest(string? CurrentPassword, string NewPassword, string? Code);

    public record AdminResetPasswordRequest(string TemporaryPassword);

    /// <summary>
    /// Authenticates a user and returns a JWT on success.  The caller must provide a valid
    /// email and password.  The response includes the token, the user's role and any
    /// associated branch identifier.  On failure a 401 response is returned.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var record = await _db.Users
            .AsNoTracking()
            .Where(u => u.Email == req.Email)
            .Select(u => new { u.Id, u.Email, u.PasswordHash, u.Role, u.BranchId, u.MustChangePassword })
            .FirstOrDefaultAsync(ct);
        if (record == null || !_auth.VerifyPassword(req.Password, record.PasswordHash))
        {
            return Unauthorized();
        }
        var user = new User
        {
            Id = record.Id,
            Email = record.Email,
            PasswordHash = record.PasswordHash,
            Role = record.Role,
            BranchId = record.BranchId,
            MustChangePassword = record.MustChangePassword
        };
        var token = _auth.GenerateJwtToken(user);
        return Ok(new { token, role = user.Role.ToString(), userId = user.Id, branchId = user.BranchId, mustChangePassword = user.MustChangePassword });
    }

    [HttpPost("me/change-password/request")]
    [Authorize]
    public async Task<IActionResult> RequestPasswordChangeCode(CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }
        var user = await _db.Users.FindAsync(new object?[] { userId }, cancellationToken: ct);
        if (user is null)
        {
            return Unauthorized();
        }

        await InvalidateTokensAsync(user.Id, PasswordResetPurpose.ChangePasswordMfa, ct);

        var code = GenerateVerificationCode();
        var token = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CodeHash = _auth.HashPassword(code),
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.AddMinutes(10),
            IsUsed = false,
            Purpose = PasswordResetPurpose.ChangePasswordMfa
        };
        _db.PasswordResetTokens.Add(token);
        await _db.SaveChangesAsync(ct);

        var subject = "ClientFlow verification code";
        var htmlBody = $"<p>Hello,</p><p>Your verification code is <strong>{code}</strong>. The code expires in 10 minutes.</p><p>If you did not request this code you can ignore this message.</p>";
        await _email.SendAsync(new[] { user.Email }, subject, htmlBody, ct);

        return NoContent();
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
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(callerId, out var creatorGuid))
        {
            createdBy = creatorGuid;
        }

        var mustChangePassword = role == UserRole.BranchAdmin;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim(),
            PasswordHash = _auth.HashPassword(password),
            Role = role,
            BranchId = branchId,
            MustChangePassword = mustChangePassword,
            CreatedByUserId = createdBy
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        if (role == UserRole.BranchAdmin)
        {
            string? branchName = null;
            if (branchId.HasValue)
            {
                branchName = await _db.Branches
                    .AsNoTracking()
                    .Where(b => b.Id == branchId.Value)
                    .Select(b => b.Name)
                    .FirstOrDefaultAsync(ct);
            }
            var baseUrl = GetAppBaseUrl();
            var loginUrl = string.IsNullOrWhiteSpace(baseUrl) ? "login.html" : $"{baseUrl}/login.html";
            var changeUrl = string.IsNullOrWhiteSpace(baseUrl) ? "admin/change-password.html" : $"{baseUrl}/admin/change-password.html";
            var subject = "ClientFlow branch manager account";
            var branchLabel = string.IsNullOrWhiteSpace(branchName) ? string.Empty : $" for <strong>{System.Net.WebUtility.HtmlEncode(branchName)}</strong>";
            var htmlBody = $"<p>Hello,</p><p>You have been added as a branch manager{branchLabel} in ClientFlow.</p><p>Sign in at <a href=\"{loginUrl}\">{loginUrl}</a> with the temporary password provided to you and then change it using the secure form at <a href=\"{changeUrl}\">{changeUrl}</a>.</p><p>If you did not expect this email please contact your administrator immediately.</p>";
            await _email.SendAsync(new[] { user.Email }, subject, htmlBody, ct);
        }
        return Created($"/api/users/{user.Id}", new { user.Id, user.Email, role = user.Role.ToString(), user.BranchId, user.MustChangePassword });
    }

    [HttpPost("me/change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        if (req is null)
        {
            return BadRequest("Request body is required.");
        }
        if (string.IsNullOrWhiteSpace(req.NewPassword))
        {
            return BadRequest("New password is required.");
        }
        if (req.NewPassword.Length < 8)
        {
            return BadRequest("New password must be at least 8 characters long.");
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }
        var user = await _db.Users.FindAsync(new object?[] { userId }, cancellationToken: ct);
        if (user is null)
        {
            return Unauthorized();
        }
        bool requiresCode = user.Role == UserRole.BranchAdmin || user.MustChangePassword;
        PasswordResetToken? token = null;
        bool hasValidCode = false;
        if (requiresCode || !string.IsNullOrWhiteSpace(req.Code))
        {
            if (string.IsNullOrWhiteSpace(req.Code))
            {
                return BadRequest("A verification code is required.");
            }
            var now = DateTime.UtcNow;
            token = await _db.PasswordResetTokens
                .Where(x => x.UserId == user.Id && x.Purpose == PasswordResetPurpose.ChangePasswordMfa && !x.IsUsed && x.ExpiresUtc >= now)
                .OrderByDescending(x => x.CreatedUtc)
                .FirstOrDefaultAsync(ct);
            if (token is null || !_auth.VerifyPassword(req.Code, token.CodeHash))
            {
                return BadRequest("Verification code is invalid or expired.");
            }
            hasValidCode = true;
        }

        if (!hasValidCode)
        {
            if (string.IsNullOrWhiteSpace(req.CurrentPassword))
            {
                return BadRequest("Current password is required.");
            }
            if (!_auth.VerifyPassword(req.CurrentPassword, user.PasswordHash))
            {
                return BadRequest("Current password is incorrect.");
            }
        }

        user.PasswordHash = _auth.HashPassword(req.NewPassword);
        user.MustChangePassword = false;
        if (token is not null)
        {
            token.IsUsed = true;
        }
        await InvalidateTokensAsync(user.Id, PasswordResetPurpose.ChangePasswordMfa, ct);
        await _db.SaveChangesAsync(ct);

        var subject = "ClientFlow password changed";
        var htmlBody = "<p>Hello,</p><p>The password for your ClientFlow account was changed successfully. If you did not perform this action please contact your administrator immediately.</p>";
        await _email.SendAsync(new[] { user.Email }, subject, htmlBody, ct);

        return NoContent();
    }

    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] AdminResetPasswordRequest req, CancellationToken ct)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.TemporaryPassword))
        {
            return BadRequest("TemporaryPassword is required.");
        }
        if (req.TemporaryPassword.Length < 8)
        {
            return BadRequest("Temporary password must be at least 8 characters long.");
        }

        var callerRole = User.FindFirstValue(ClaimTypes.Role);
        var user = await _db.Users.FindAsync(new object?[] { id }, cancellationToken: ct);
        if (user is null)
        {
            return NotFound();
        }
        if (user.Role == UserRole.SuperAdmin && callerRole != UserRole.SuperAdmin.ToString())
        {
            return Forbid();
        }

        user.PasswordHash = _auth.HashPassword(req.TemporaryPassword);
        user.MustChangePassword = true;
        await InvalidateTokensAsync(user.Id, PasswordResetPurpose.ChangePasswordMfa, ct);
        await _db.SaveChangesAsync(ct);

        var baseUrl = GetAppBaseUrl();
        var changeUrl = string.IsNullOrWhiteSpace(baseUrl) ? "admin/change-password.html" : $"{baseUrl}/admin/change-password.html";
        var subject = "ClientFlow password reset";
        var htmlBody = $"<p>Hello,</p><p>An administrator reset the password for your ClientFlow account. Use the new temporary password provided to you and change it promptly at <a href=\"{changeUrl}\">{changeUrl}</a>.</p><p>If you did not request this change contact your administrator.</p>";
        await _email.SendAsync(new[] { user.Email }, subject, htmlBody, ct);

        return NoContent();
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

    private async Task InvalidateTokensAsync(Guid userId, PasswordResetPurpose purpose, CancellationToken ct)
    {
        var tokens = await _db.PasswordResetTokens
            .Where(x => x.UserId == userId && x.Purpose == purpose && !x.IsUsed)
            .ToListAsync(ct);
        if (tokens.Count == 0) return;
        foreach (var t in tokens)
        {
            t.IsUsed = true;
        }
    }

    private static string GenerateVerificationCode()
    {
        var bytes = new byte[4];
        RandomNumberGenerator.Fill(bytes);
        var value = BitConverter.ToUInt32(bytes, 0) % 1_000_000U;
        return value.ToString("D6");
    }

    private string GetAppBaseUrl()
    {
        var request = HttpContext?.Request;
        if (request is null || !request.Host.HasValue)
        {
            return string.Empty;
        }
        var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
        return baseUrl.TrimEnd('/');
    }
}