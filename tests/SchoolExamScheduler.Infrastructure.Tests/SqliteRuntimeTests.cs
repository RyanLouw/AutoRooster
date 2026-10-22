using Microsoft.Data.Sqlite;
using Xunit;
public sealed class SqliteRuntimeTests
{
    [Fact]
    public async Task NativeSqliteOpensAndExecutesQueriesWithoutServer()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys";
        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }
}
