using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.Core.Utils;
using NAudio.Wave;
using System.Timers;

namespace KotonohaAssistant.AI.Services;

public interface IAlarmService
{
    public void Start();
    public void Stop();
    public void StopAlarm();
    public Task<bool> SetAlarm(AlarmSetting setting);
}

public class AlarmService : IDisposable, IAlarmService
{
    /// <summary>
    /// タイマーが強制的に終了するまでの時間
    /// </summary>
    public static readonly TimeSpan MaxCallingTime = TimeSpan.FromSeconds(60);

    /// <summary>
    /// タイマーのチェック間隔
    /// </summary>
    public static readonly TimeSpan TimerCheckInterval = TimeSpan.FromSeconds(30);

    private readonly VoiceClient _voiceClient;
    private readonly System.Timers.Timer _timer;
    private readonly ElapsedEventHandler _onTimeElapsed;
    private readonly IAlarmRepository _alarmRepository;
    private bool _calling = false;

    /// <summary>
    /// 処理中のアラームのキャンセルトークン
    /// </summary>
    private CancellationTokenSource? _cts;

    public AlarmService(IAlarmRepository alarmRepository)
    {
        _voiceClient = new VoiceClient();
        _alarmRepository = alarmRepository;

        _timer = new System.Timers.Timer(MaxCallingTime);
        _onTimeElapsed = new ElapsedEventHandler(async (sender, args) => await OnTimeElapsed(sender, args));
        _timer.Elapsed += _onTimeElapsed;
    }

    /// <summary>
    /// アラームのサービスを開始します
    /// </summary>
    public void Start()
    {
        if (!_timer.Enabled)
        {
            _timer.Start();
        }
    }

    /// <summary>
    /// アラームのサービスを停止します
    /// </summary>
    public void Stop()
    {
        if (_timer.Enabled)
        {
            _timer.Stop();
        }
    }

    /// <summary>
    /// 現在鳴っているアラームを停止します
    /// </summary>
    public void StopAlarm()
    {
        _cts?.Cancel();
    }

    public async Task<bool> SetAlarm(AlarmSetting setting)
    {
        try
        {
            await _alarmRepository.InsertAlarmSetting(setting);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task OnTimeElapsed(object? sender, ElapsedEventArgs args)
    {
        // 他のアラームが鳴り続けている場合はスキップする
        if (_calling)
        {
            return;
        }

        var startTime = DateTime.Now;
        var settings = await _alarmRepository.GetAlarmSettingsAsync(startTime.TimeOfDay - MaxCallingTime, startTime.TimeOfDay);
        if (settings is [])
        {
            return;
        }

        // 複数設定があっても1個だけしか使わない
        var alarm = settings[0];
        if (string.IsNullOrWhiteSpace(alarm.Message))
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _cts.CancelAfter(MaxCallingTime);

        _calling = true;
        try
        {
            // 3秒アラーム音声＋メッセージ読み上げを繰り返し
            var end = startTime + MaxCallingTime;
            while (DateTime.Now < end && !_cts.Token.IsCancellationRequested)
            {
                await PlayAlarmSoundAsync(TimeSpan.FromSeconds(5), _cts.Token);
                await _voiceClient.SpeakAsync(alarm.Sister, Core.Emotion.Calm, alarm.Message);
            }
        }
        catch(OperationCanceledException)
        {
            // キャンセル時の処理
        }
        finally
        {
            _calling = false;
            _cts.Dispose();
            _cts = null;
        }

        await _alarmRepository.DeleteAlarmSettingsAsync(settings.Select(s => s.Id));
    }

    static async Task PlayAlarmSoundAsync(TimeSpan playingTime, CancellationToken token)
    {
        var path = @"D:\Windows\Programs\csharp\KotonohaAssistant\assets\Clock-Alarm02-1(Loop).mp3";
        using var audioFile = new AudioFileReader(path);
        using var outputDevice = new WaveOutEvent();

        try
        {
            outputDevice.Init(audioFile);

            outputDevice.Play();

            await Task.Delay(playingTime, token);
        }
        catch(Exception)
        {
            outputDevice.Stop();
        }
    }

    public void Dispose()
    {
        _voiceClient.Dispose();
        _timer.Elapsed -= _onTimeElapsed;
        _timer.Dispose();
        _cts?.Dispose();
    }
}