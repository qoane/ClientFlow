using System.Collections.Generic;

namespace ClientFlow.Domain.Users;

/// <summary>
/// Represents an authenticated user of the admin system.  Users have roles
/// that control which resources they can access.  Branch admins may only
/// manage their assigned branch, whereas Admins and SuperAdmins can manage
/// all branches.
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    /// <summary>
    /// A salted and hashed password.  Passwords are not stored in plain text.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.BranchAdmin;
    /// <summary>
    /// Optional branch assignment.  BranchAdmins are limited to this branch.
    /// Null for Admin and SuperAdmin roles.
    /// </summary>
    public Guid? BranchId { get; set; }
    public ClientFlow.Domain.Branches.Branch? Branch { get; set; }

    /// <summary>
    /// When true the user must change their password before accessing the
    /// remainder of the administration experience.  This flag is typically
    /// set when an administrator creates a new BranchAdmin account or when
    /// a password reset is performed on behalf of the user.
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// Tracks which administrator created this account.  Null when the
    /// account is seeded or created by the system itself.  The navigation
    /// property enables auditing screens to show who invited each user.
    /// </summary>
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    /// <summary>
    /// Collection of users that were created by this administrator.  EF Core
    /// uses this navigation to build the self-referencing relationship defined
    /// in the migrations.
    /// </summary>
    public ICollection<User> CreatedUsers { get; set; } = new List<User>();
}

public enum UserRole
{
    BranchAdmin = 0,
    Admin = 1,
    SuperAdmin = 2
}