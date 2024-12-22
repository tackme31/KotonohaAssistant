using KotonohaAssistant.Core.Utils;
using NAudio.Wave;
using System.Timers;

namespace KotonohaAssistant.Alarm;

public class AlarmService : IDisposable
{
    public static readonly TimeSpan TimerInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan SoundInterval = TimeSpan.FromSeconds(2);

    //private readonly VoiceClient _voiceClient;
    private readonly System.Timers.Timer _timer;
    private readonly ElapsedEventHandler _onTimeElapsed;
    private readonly IAlarmRepository _alarmRepository;
    private bool _calling = false;

    public AlarmService(IAlarmRepository alarmRepository)
    {
        //_voiceClient = new VoiceClient();
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
        // 他のアラームが鳴り続けている場合はスキップする
        if (_calling)
        {
            Console.WriteLine("Skip: 他のアラームを再生中です。");
            return;
        }

        var startTime = DateTime.Now;
        try
        {
            var targetSettings = await _alarmRepository.GetAlarmSettingsAsync(startTime.TimeOfDay - TimerInterval, startTime.TimeOfDay);
            if (targetSettings is [])
            {
                Console.WriteLine("Skip: アラーム設定がありません。");
                return;
            }

            _calling = true;

            while (DateTime.Now - startTime < TimerInterval)
            {
                await PlayAlarmSoundAsync();

                if (!(await HasAlarmSetting(startTime.TimeOfDay - TimerInterval, startTime.TimeOfDay)))
                {
                    break;
                }

                /*var message = targetSettings[0].Message;
                if (!string.IsNullOrWhiteSpace(message))
                {
                    await _voiceClient.SpeakAsync(targetSettings[0].Sister, message);
                }

                if (!(await HasAlarmSetting(startTime.TimeOfDay - TimerInterval, startTime.TimeOfDay)))
                {
                    break;
                }*/
            }

            _calling = false;

            await _alarmRepository.DeleteAlarmSettingsAsync(targetSettings.Select(s => s.Id));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        async Task<bool> HasAlarmSetting(TimeSpan from, TimeSpan to)
        {
            var settings = await _alarmRepository.GetAlarmSettingsAsync(from, to);
            return settings is not [];
        }
    }

    static async Task PlayAlarmSoundAsync()
    {
        var path = @"D:\Windows\Programs\csharp\KotonohaAssistant\assets\Clock-Alarm02-1(Loop).mp3";
        using var audioFile = new AudioFileReader(path);
        using var outputDevice = new WaveOutEvent();

        outputDevice.Init(audioFile);
        outputDevice.Play();

        await Task.Delay(SoundInterval);

        outputDevice.Stop();
    }

    public void Dispose()
    {
        _timer.Elapsed -= _onTimeElapsed;

        _timer.Dispose();
    }
}