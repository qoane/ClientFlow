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
    EXEC(N'ALTER TABLE dbo.Users ADD MustChangePassword BIT NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT(0);');
    EXEC(N'UPDATE dbo.Users SET MustChangePassword = 0 WHERE MustChangePassword IS NULL;');
    EXEC(N'ALTER TABLE dbo.Users DROP CONSTRAINT DF_Users_MustChangePassword;');
END";

        // Execute the remediation script outside of migrations so environments
        // that missed the original migration can heal automatically.
        db.Database.ExecuteSqlRaw(sql);
    }

    /// <summary>
    /// Ensures the <c>CreatedByUserId</c> column and its supporting constraints
    /// exist on the <c>Users</c> table. This column was introduced after some
    /// customer databases were provisioned, leaving them without the column even
    /// though the application now depends on it. Running this remediation keeps
    /// user creation resilient in those environments.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public static void EnsureCreatedByUserIdColumn(AppDbContext db)
    {
        const string sql = @"IF COL_LENGTH('dbo.Users','CreatedByUserId') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
END

IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_CreatedByUserId' AND object_id = OBJECT_ID('dbo.Users'))
        CREATE INDEX IX_Users_CreatedByUserId ON dbo.Users(CreatedByUserId);

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_Users_CreatedByUserId')
    BEGIN
        ALTER TABLE dbo.Users  WITH CHECK
        ADD CONSTRAINT FK_Users_Users_CreatedByUserId
        FOREIGN KEY(CreatedByUserId) REFERENCES dbo.Users(Id)
        ON DELETE NO ACTION;
    END
END";

        db.Database.ExecuteSqlRaw(sql);
    }

    /// <summary>
    /// Ensures the <c>PasswordResetTokens</c> table exists with the expected schema.
    /// Several legacy environments were provisioned before password reset support was
    /// introduced, leaving them without the new table.  Because those databases also
    /// have empty migration histories, relying solely on EF migrations is not
    /// sufficient.  Executing this remediation on startup guarantees that password
    /// reset flows function even when the migration history is incomplete.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public static void EnsurePasswordResetTokensTable(AppDbContext db)
    {
        const string sql = @"IF OBJECT_ID(N'dbo.PasswordResetTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetTokens
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PasswordResetTokens_Id DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        CodeHash NVARCHAR(256) NOT NULL,
        CreatedUtc DATETIME2 NOT NULL,
        ExpiresUtc DATETIME2 NOT NULL,
        IsUsed BIT NOT NULL CONSTRAINT DF_PasswordResetTokens_IsUsed DEFAULT(0),
        Purpose INT NOT NULL,
        CONSTRAINT PK_PasswordResetTokens PRIMARY KEY (Id)
    );

    CREATE INDEX IX_PasswordResetTokens_UserId ON dbo.PasswordResetTokens(UserId);

    ALTER TABLE dbo.PasswordResetTokens WITH CHECK
        ADD CONSTRAINT FK_PasswordResetTokens_Users_UserId
        FOREIGN KEY(UserId) REFERENCES dbo.Users(Id)
        ON DELETE CASCADE;
END";

        db.Database.ExecuteSqlRaw(sql);
    }
}
