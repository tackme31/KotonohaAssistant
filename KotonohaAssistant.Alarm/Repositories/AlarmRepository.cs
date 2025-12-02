using Dapper;
using KotonohaAssistant.Alarm.Models;
using Microsoft.Data.Sqlite;
using System.Data;

namespace KotonohaAssistant.Alarm.Repositories;

public interface IAlarmRepository
{
    Task<List<AlarmSetting>> GetAlarmSettingsAsync(TimeSpan from, TimeSpan to);

    Task DeleteAlarmSettingsAsync(IEnumerable<long> ids);

    Task<long> InsertAlarmSettingAsync(AlarmSetting setting);
    Task UpdateIsEnabledAsync(long id, bool isEnabled);
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
    VoicePath TEXT NOT NULL,
    IsEnabled INTEGER NOT NULL,
    IsRepeated INTEGER NOT NULL
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

    public async Task<long> InsertAlarmSettingAsync(AlarmSetting setting)
    {
        await InitializeDatabaseAsync();

        var sql = @"
INSERT INTO AlarmSetting
    (TimeInSeconds, VoicePath, IsEnabled, IsRepeated)
VALUES
    (@TimeInSeconds, @VoicePath, @IsEnabled, @IsRepeated);

SELECT last_insert_rowid();
";

        try
        {
            using var connection = Connection;
            connection.Open();

            var id = await connection.QuerySingleAsync<long>(sql, setting);
            return id;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return -1;
        }
    }


    public async Task UpdateIsEnabledAsync(long id, bool isEnabled)
    {
        await InitializeDatabaseAsync();

        var sql = "UPDATE AlarmSetting SET IsEnabled = @IsEnabled WHERE Id = @Id";

        try
        {
            using var connection = Connection;
            connection.Open();

            _ = await connection.ExecuteAsync(sql, new
            {
                IsEnabled = isEnabled,
                Id = id
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
