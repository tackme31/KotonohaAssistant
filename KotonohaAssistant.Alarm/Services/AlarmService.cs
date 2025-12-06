using System.IO;
using System.Timers;
using KotonohaAssistant.Alarm.Models;
using KotonohaAssistant.Alarm.Repositories;
using KotonohaAssistant.Core.Utils;
using NAudio.Wave;

namespace KotonohaAssistant.Alarm.Services;

public interface IAlarmService
{
    public void Start();
    public void Stop();
    public void StopAlarm();
    public Task<bool> SetAlarm(AlarmSetting setting);
    public long? GetCurrentAlarmId();
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
    public static readonly TimeSpan TimerCheckInterval = TimeSpan.FromSeconds(15);

    private readonly System.Timers.Timer _timer;
    private readonly ElapsedEventHandler _onTimeElapsed;
    private readonly IAlarmRepository _alarmRepository;
    private readonly string _alarmSoundFile;
    private readonly ILogger _logger;
    private bool _calling = false;
    private long? _currentAlabrmId = null;

    /// <summary>
    /// 処理中のアラームのキャンセルトークン
    /// </summary>
    private CancellationTokenSource? _cts;

    public AlarmService(IAlarmRepository alarmRepository, string alarmSoundFile, ILogger logger)
    {
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
            await _alarmRepository.InsertAlarmSettingAsync(setting);
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
            settings = await _alarmRepository.GetAlarmSettingsAsync(startTime.TimeOfDay - TimeSpan.FromMinutes(1), startTime.TimeOfDay);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
        }

        var enabledSettings = settings?.Where(s => s.IsEnabled).ToList();
        if (enabledSettings is null or [])
        {
            return;
        }

        // 複数設定があっても1個だけしか使わない
        var alarm = enabledSettings[0];
        if (!File.Exists(alarm.VoicePath))
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _cts.CancelAfter(MaxCallingTime);

        _calling = true;
        _currentAlabrmId = alarm.Id;
        try
        {
            // アラーム音声3秒
            await PlayAlarmSoundAsync(_alarmSoundFile, TimeSpan.FromSeconds(3), _cts.Token);

            // ボイス音声読み上げ
            await PlayAlarmSoundAsync(alarm.VoicePath, TimeSpan.FromSeconds(10), _cts.Token);

            // 残りはアラーム音声の繰り返し
            var end = startTime + MaxCallingTime;
            while (DateTime.Now < end && !_cts.Token.IsCancellationRequested)
            {
                await PlayAlarmSoundAsync(_alarmSoundFile, TimeSpan.FromSeconds(5), _cts.Token);
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
        catch (OperationCanceledException)
        {
            // キャンセル時の処理
            _logger.LogInformation("タイマーを停止しました");
        }
        finally
        {
            _calling = false;
            _currentAlabrmId = null;
            _cts.Dispose();
            _cts = null;
        }

        try
        {
            await _alarmRepository.UpdateIsEnabledAsync(alarm.Id, isEnabled: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
        }
    }

    async Task PlayAlarmSoundAsync(string audioFilePath, TimeSpan playingTime, CancellationToken token)
    {
        using var audioFile = new AudioFileReader(audioFilePath);
        using var outputDevice = new WaveOutEvent();

        var tcs = new TaskCompletionSource();
        outputDevice.PlaybackStopped += (s, e) =>
        {
            tcs.TrySetResult();
        };

        try
        {
            outputDevice.Init(audioFile);
            outputDevice.Play();

            var delayTask = Task.Delay(playingTime, token);
            var completedTask = await Task.WhenAny(tcs.Task, delayTask);
            if (completedTask == delayTask)
            {
                outputDevice.Stop();
            }

            token.ThrowIfCancellationRequested();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
            outputDevice.Stop();
        }
    }

    public void Dispose()
    {
        _timer.Elapsed -= _onTimeElapsed;
        _timer.Dispose();
        _cts?.Dispose();
    }

    public long? GetCurrentAlarmId() => _currentAlabrmId;
}
