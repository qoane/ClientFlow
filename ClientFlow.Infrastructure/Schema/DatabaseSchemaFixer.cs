using ClientFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClientFlow.Infrastructure.Schema;

/// <summary>
/// Contains targeted schema remediation helpers that can be executed at runtime
/// to recover from environments where older databases are missing recent columns.
/// </summary>
public static class DatabaseSchemaFixer
{
    /// <summary>
    /// Ensures the <c>MustChangePassword</c> column exists on the <c>Users</c> table.
    /// Some customer environments were provisioned before this column existed and
    /// have an empty migrations history that prevents EF from adding it.  Running
    /// this check on startup keeps the login flow resilient by creating the column
    /// when it is absent.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public static void EnsureMustChangePasswordColumn(AppDbContext db)
    {
        const string sql = @"IF COL_LENGTH('dbo.Users','MustChangePassword') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD MustChangePassword BIT NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT(0);
    UPDATE dbo.Users SET MustChangePassword = 0 WHERE MustChangePassword IS NULL;
    ALTER TABLE dbo.Users DROP CONSTRAINT DF_Users_MustChangePassword;
END";

        // Execute the remediation script outside of migrations so environments
        // that missed the original migration can heal automatically.
        db.Database.ExecuteSqlRaw(sql);
    }
}
