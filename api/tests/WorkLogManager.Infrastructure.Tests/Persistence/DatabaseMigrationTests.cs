using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkLogManager.Infrastructure.Persistence;

namespace WorkLogManager.Infrastructure.Tests.Persistence;

public class DatabaseMigrationTests : IDisposable
{
    private readonly string _databasePath;

    public DatabaseMigrationTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"worklogmanager-migration-tests-{Guid.NewGuid():N}.db");
    }

    [Theory]
    [InlineData("employees")]
    [InlineData("employee_work_logs")]
    [InlineData("work_schedule_periods")]
    [InlineData("month_closings")]
    [InlineData("system_parameters")]
    public async Task Migrate_OnFreshSqliteDatabase_CreatesExpectedTable(string tableName)
    {
        var options = new DbContextOptionsBuilder<WorkLogManagerDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .UseSnakeCaseNamingConvention()
            .Options;

        await using (var context = new WorkLogManagerDbContext(options))
        {
            context.Database.Migrate();
        }

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        command.Parameters.AddWithValue("$tableName", tableName);

        var result = await command.ExecuteScalarAsync();

        Assert.Equal(tableName, result);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
