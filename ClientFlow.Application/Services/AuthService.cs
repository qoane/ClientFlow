using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ClientFlow.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClientFlow.Application.Services;

/// <summary>
/// Provides helper methods for hashing passwords and generating JSON Web Tokens (JWTs) for
/// authenticated users.  Passwords are hashed using SHA256 and encoded in base64 to
/// avoid storing them in plain text.  JWTs are signed with a symmetric key defined
/// in <see cref="JwtSettings"/>.
/// </summary>
public class AuthService
{
    private readonly JwtSettings _jwt;

    public AuthService(IOptions<JwtSettings> jwtOptions)
    {
        _jwt = jwtOptions.Value;
    }

    /// <summary>
    /// Hashes the specified plain text password using SHA256 and returns the base64
    /// representation of the digest.  The same hashing algorithm is used when seeding
    /// initial users in the database.
    /// </summary>
    public string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Verifies that the specified plain text password corresponds to the given hashed
    /// value.  A constant‑time comparison is used to mitigate timing attacks.
    /// </summary>
    public bool VerifyPassword(string password, string hashed)
    {
        var hashOfInput = HashPassword(password);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(hashOfInput),
            Encoding.UTF8.GetBytes(hashed));
    }

    /// <summary>
    /// Generates a signed JSON Web Token for the specified user.  The token includes
    /// standard claims such as sub (user ID) and email, along with a role claim
    /// representing the user's role.  If the user is associated with a branch the
    /// BranchId claim is also included.  The token expiry is determined by
    /// <see cref="JwtSettings.ExpiryMinutes"/>.
    /// </summary>
    public string GenerateJwtToken(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        if (user.BranchId.HasValue)
        {
            claims.Add(new Claim("BranchId", user.BranchId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_jwt.ExpiryMinutes);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}