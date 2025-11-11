using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestoreMustChangePasswordColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Users','MustChangePassword') IS NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.Users ADD MustChangePassword BIT NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT(0);');
    EXEC(N'UPDATE dbo.Users SET MustChangePassword = 0 WHERE MustChangePassword IS NULL;');
    EXEC(N'ALTER TABLE dbo.Users DROP CONSTRAINT DF_Users_MustChangePassword;');
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Users','MustChangePassword') IS NOT NULL
BEGIN
    DECLARE @ConstraintName NVARCHAR(128);
    SELECT @ConstraintName = df.name
    FROM sys.default_constraints df
    INNER JOIN sys.columns c ON c.default_object_id = df.object_id
    WHERE df.parent_object_id = OBJECT_ID('dbo.Users') AND c.name = 'MustChangePassword';
    IF @ConstraintName IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE dbo.Users DROP CONSTRAINT [' + @ConstraintName + ']');
    END
    EXEC(N'ALTER TABLE dbo.Users DROP COLUMN MustChangePassword;');
END
");
        }
    }
}
