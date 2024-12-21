using Dapper;
using KotonohaAssistant.Core.Models;
using KotonohaAssistant.Core.Utils;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Timers;

namespace KotonohaAssistant.Alarm;

internal class AlarmService : IDisposable
{
    private static readonly string AppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kotonoha Assistant");
    private static readonly string DBPath = Path.Combine(AppFolder, "alarm.db");

    private static readonly TimeSpan TimerInterval = TimeSpan.FromSeconds(10);

    private readonly VoiceClient _voiceClient;
    private readonly System.Timers.Timer _timer;
    private readonly ElapsedEventHandler _onTimeElapsed;

    private IDbConnection Connection => new SqliteConnection($"Data Source={DBPath}");
    private IDbConnection ReadOnlyConnection => new SqliteConnection($"Data Source={DBPath};Mode=ReadOnly");

    public AlarmService()
    {
        _voiceClient = new VoiceClient();

        _timer = new System.Timers.Timer(TimerInterval);
        _onTimeElapsed = new ElapsedEventHandler(async (sender, args) => await OnTimeElapsed(sender, args));
        _timer.Elapsed += _onTimeElapsed;
    }

    private async Task InitializeDatabaseAsync()
    {
        using var connection = Connection;

        connection.Open();

        var createTableQuery = @"
CREATE TABLE IF NOT EXISTS AlarmSetting (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TimeInSeconds INTEGER NOT NULL,
    Sister INTEGER NOT NULL,
    Message TEXT NOT NULL
);";
        await connection.ExecuteAsync(createTableQuery);
    }

    public async Task Start()
    {
        await InitializeDatabaseAsync();

        using var connection = Connection;
        connection.Open();

        var setting = new AlarmSetting
        {
            TimeInSeconds = (DateTime.Now.TimeOfDay + TimerInterval).TotalSeconds,
            Sister = Core.Kotonoha.Aoi,
            Message = "マスター、朝だよ。早く起きないと遅刻するよ。",
        };
        var sql = @"
INSERT INTO AlarmSetting (TimeInSeconds, Sister, Message)
VALUES (@TimeInSeconds, @Sister, @Message)
";
        try
        {
            await connection.ExecuteAsync(sql, setting);
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        _timer.Start();
    }

    private async Task OnTimeElapsed(object? sender, ElapsedEventArgs args)
    {
        var now = DateTime.Now.TimeOfDay;
        var settings = await GetAlarmSettingsAsync(now);
        if (settings is [])
        {
            Console.WriteLine("アラーム設定がありません");
        }

        // アラーム削除
        await DeleteAlarmSettingsAsync(settings.Select(s => s.Id));

        try
        {
            foreach (var setting in settings)
            {
                if (string.IsNullOrEmpty(setting.Message))
                {
                    continue;
                }

                await _voiceClient.SpeakAsync(setting.Sister, setting.Message);
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private async Task<List<AlarmSetting>> GetAlarmSettingsAsync(TimeSpan now)
    {
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
                From = (now - TimerInterval).TotalSeconds,
                To = now.TotalSeconds,
            });

            return settings.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return [];
        }
    }

    private async Task DeleteAlarmSettingsAsync(IEnumerable<long> ids)
    {
        var sql = "DELETE FROM AlarmSetting WHERE Id IN @Ids";
        try
        {
            using var connection = Connection;
            connection.Open();

            _ = connection.Execute(sql, new { Ids = ids });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public void Dispose()
    {
        _timer.Elapsed -= _onTimeElapsed;

        _timer.Dispose();
        _voiceClient.Dispose();
    }
}
