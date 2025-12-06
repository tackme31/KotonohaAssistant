using System.Data;
using Dapper;
using KotonohaAssistant.Core.Utils;
using Microsoft.Data.Sqlite;

namespace KotonohaAssistant.AI.Repositories;

public interface IAlarmRepository
{
    Task<List<AlarmSetting>> GetAlarmSettingsAsync(TimeSpan from, TimeSpan to);

    Task DeleteAlarmSettingsAsync(IEnumerable<long> ids);

    Task InsertAlarmSetting(AlarmSetting setting);
}

public class AlarmRepository(string dbPath) : IAlarmRepository
{
    private bool _isInitialized = false;

    private IDbConnection Connection => new SqliteConnection($"Data Source={dbPath}");
    private IDbConnection ReadOnlyConnection => new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");

    private async Task InitializeDatabaseAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        using var connection = Connection;

        connection.Open();

        var createTableQuery = @"
CREATE TABLE IF NOT EXISTS AlarmSetting (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TimeInSeconds INTEGER NOT NULL,
    Sister INTEGER NOT NULL,
    Message TEXT NOT NULL
);
";
        await connection.ExecuteAsync(createTableQuery);

        _isInitialized = true;
    }

    public async Task<List<AlarmSetting>> GetAlarmSettingsAsync(TimeSpan from, TimeSpan to)
    {
        await InitializeDatabaseAsync();

        var sql = @"
SELECT *
FROM AlarmSetting
WHERE @From < TimeInSeconds
  AND TimeInSeconds < @To
";
        try
        {
            using var connection = ReadOnlyConnection;
            connection.Open();
            var settings = await connection.QueryAsync<AlarmSetting>(sql, new
            {
                From = from.TotalSeconds,
                To = to.TotalSeconds,
            });

            return settings.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return [];
        }
    }

    public async Task DeleteAlarmSettingsAsync(IEnumerable<long> ids)
    {
        await InitializeDatabaseAsync();

        var sql = "DELETE FROM AlarmSetting WHERE Id IN @Ids";
        try
        {
            using var connection = Connection;
            connection.Open();

            _ = await connection.ExecuteAsync(sql, new { Ids = ids });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public async Task InsertAlarmSetting(AlarmSetting setting)
    {
        await InitializeDatabaseAsync();

        var sql = @"
INSERT INTO AlarmSetting
    (TimeInSeconds, Sister, Message)
VALUES
    (@TimeInSeconds, @Sister, @Message)
";

        try
        {
            using var connection = Connection;
            connection.Open();

            _ = await connection.ExecuteAsync(sql, setting);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
