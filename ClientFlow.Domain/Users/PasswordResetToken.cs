using System;

namespace ClientFlow.Domain.Users;

/// <summary>
/// Represents a short-lived verification code that can be used to
/// complete password changes or administrator initiated resets.
/// Codes are hashed before being stored to avoid keeping the raw value.
/// </summary>
public class PasswordResetToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public bool IsUsed { get; set; }
    public PasswordResetPurpose Purpose { get; set; } = PasswordResetPurpose.ChangePasswordMfa;
    public User User { get; set; } = null!;
}

public enum PasswordResetPurpose
{
    ChangePasswordMfa = 0,
    AdminReset = 1
}
