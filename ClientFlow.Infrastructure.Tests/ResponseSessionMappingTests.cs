using ClientFlow.Domain.Surveys;
using ClientFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ClientFlow.Infrastructure.Tests;

public class ResponseSessionMappingTests
{
    [Fact]
    public void ResponseSession_Uses_Legacy_Table_Name()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(ResponseSession));

        Assert.NotNull(entityType);
        var tableName = entityType!
            .GetTableMappings()
            .Single()
            .Table
            .Name;
        Assert.Equal("tblSessions", tableName);
    }
}
