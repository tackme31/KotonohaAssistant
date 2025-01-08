using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.Core.Utils;
using NAudio.Wave;
using SQLitePCL;
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
    private readonly string _alarmSoundFile;
    private readonly ILogger _logger;
    private bool _calling = false;

    /// <summary>
    /// 処理中のアラームのキャンセルトークン
    /// </summary>
    private CancellationTokenSource? _cts;

    public AlarmService(IAlarmRepository alarmRepository, string alarmSoundFile, ILogger logger)
    {
        _voiceClient = new VoiceClient();
        _alarmRepository = alarmRepository;
        _alarmSoundFile = alarmSoundFile;
        _logger = logger;

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
        catch (Exception ex)
        {
            _logger.LogError(ex);
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
        List<AlarmSetting>? settings = null;
        try
        {
            settings = await _alarmRepository.GetAlarmSettingsAsync(startTime.TimeOfDay - MaxCallingTime, startTime.TimeOfDay);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
        }

        if (settings is null or [])
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
            // アラーム音声3秒 + 読み上げ
            await PlayAlarmSoundAsync(TimeSpan.FromSeconds(3), _cts.Token);
            await _voiceClient.SpeakAsync(alarm.Sister, Core.Emotion.Calm, alarm.Message);

            // 残りはアラーム音声の繰り返し
            var end = startTime + MaxCallingTime;
            while (DateTime.Now < end && !_cts.Token.IsCancellationRequested)
            {
                await PlayAlarmSoundAsync(TimeSpan.FromSeconds(5), _cts.Token);
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
        catch(OperationCanceledException)
        {
            // キャンセル時の処理
            _logger.LogInformation("タイマーを停止しました");
        }
        finally
        {
            _calling = false;
            _cts.Dispose();
            _cts = null;
        }

        try
        {
            await _alarmRepository.DeleteAlarmSettingsAsync(settings.Select(s => s.Id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
        }
    }

    async Task PlayAlarmSoundAsync(TimeSpan playingTime, CancellationToken token)
    {
        using var audioFile = new AudioFileReader(_alarmSoundFile);
        using var outputDevice = new WaveOutEvent();

        try
        {
            outputDevice.Init(audioFile);

            outputDevice.Play();

            await Task.Delay(playingTime, token);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex);
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