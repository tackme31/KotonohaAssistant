using KotonohaAssistant.Core.Utils;
using NAudio.Wave;
using System.Windows;

namespace KotonohaAssistant.Alarm.Services;

public interface ITimerService
{
    event Action<int> Tick;
    event Action Finished;
    void SetTime(int seconds);
    void Start();
    void Stop();
    void StopAlarm();
}

public class TimerService : ITimerService
{
    private readonly string _alertSoundFile;
    private readonly ILogger _logger;

    private CancellationTokenSource? _timerCts;
    private CancellationTokenSource? _stopCts;

    private int _remainingSeconds;

    public event Action<int>? Tick;
    public event Action? Finished;

    public TimerService(string alertSoundFile, ILogger logger)
    {
        _alertSoundFile = alertSoundFile;
        _logger = logger;
    }

    public void SetTime(int seconds)
    {
        if (seconds <= 0)
        {
            throw new ArgumentException("'seconds' must be positive.");
        }

        _remainingSeconds = seconds;
    }

    public void Start()
    {
        if (_timerCts != null && !_timerCts.IsCancellationRequested)
        {
            return; // すでに動作中
        }

        _timerCts = new CancellationTokenSource();
        var token = _timerCts.Token;

        Task.Run(async () =>
        {
            try
            {
                SafeInvokeTick(_remainingSeconds);

                while (_remainingSeconds > 0 && !token.IsCancellationRequested)
                {
                    await Task.Delay(1000, token);
                    _remainingSeconds--;

                    if (_remainingSeconds > 0)
                    {
                        SafeInvokeTick(_remainingSeconds);
                    }
                }

                if (token.IsCancellationRequested)
                {
                    return;
                }

                // タイマー終了
                SafeInvokeFinished();
                await HandleAlarmAsync();
            }
            catch (TaskCanceledException)
            {
                // 無視
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
            }
        }, token);
    }

    public void Stop()
    {
        // タイマー停止
        _timerCts?.Cancel();

        // アラーム停止（手動）
        _stopCts?.Cancel();
    }

    public void StopAlarm()
    {
        _stopCts?.Cancel();
    }

    private void SafeInvokeTick(int value)
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            Tick?.Invoke(value);
        });
    }

    private void SafeInvokeFinished()
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            Finished?.Invoke();
        });
    }

    private async Task HandleAlarmAsync()
    {
        // 手動停止用
        _stopCts = new CancellationTokenSource();

        // 自動停止（1分後）
        var autoStopCts = new CancellationTokenSource();
        autoStopCts.CancelAfter(TimeSpan.FromMinutes(1));

        // どちらでも止められるトークン
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            _stopCts.Token,
            autoStopCts.Token
        );

        await PlayAlarmSoundAsync(_alertSoundFile, linked.Token);
    }

    private async Task PlayAlarmSoundAsync(string audioFilePath, CancellationToken token)
    {
        try
        {
            using var reader = new AudioFileReader(audioFilePath);
            using var output = new WaveOutEvent();
            output.Init(reader);

            var restartRequested = false;
            output.PlaybackStopped += (s, e) =>
            {
                restartRequested = true;
            };

            output.Play();

            while (output.PlaybackState == PlaybackState.Playing && !token.IsCancellationRequested)
            {
                await Task.Delay(100, token);

                // ループが必要なら巻き戻して再生
                if (restartRequested)
                {
                    restartRequested = false;
                    reader.Position = 0;
                    output.Play();
                }
            }

            output.Stop();
        }
        catch (TaskCanceledException)
        {
            // 手動 or 自動停止
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
        }
    }
}
