using KotonohaAssistant.Core.Utils;
using System.Data;
using System.Timers;

namespace KotonohaAssistant.Alarm;

internal class AlarmService : IDisposable
{
    private static readonly TimeSpan TimerInterval = TimeSpan.FromSeconds(10);

    private readonly VoiceClient _voiceClient;
    private readonly System.Timers.Timer _timer;
    private readonly ElapsedEventHandler _onTimeElapsed;
    private readonly IAlarmRepository _alarmRepository;

    public AlarmService(IAlarmRepository alarmRepository)
    {
        _voiceClient = new VoiceClient();
        _alarmRepository = alarmRepository;

        _timer = new System.Timers.Timer(TimerInterval);
        _onTimeElapsed = new ElapsedEventHandler(async (sender, args) => await OnTimeElapsed(sender, args));
        _timer.Elapsed += _onTimeElapsed;
    }

    public void Start()
    {
        _timer.Start();
    }

    private async Task OnTimeElapsed(object? sender, ElapsedEventArgs args)
    {
        var now = DateTime.Now.TimeOfDay;
        var settings = await _alarmRepository.GetAlarmSettingsAsync(now - TimerInterval, now);
        if (settings is [])
        {
            Console.WriteLine("アラーム設定がありません");
        }

        // アラーム削除
        await _alarmRepository.DeleteAlarmSettingsAsync(settings.Select(s => s.Id));

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

    public void Dispose()
    {
        _timer.Elapsed -= _onTimeElapsed;

        _timer.Dispose();
        _voiceClient.Dispose();
    }
}