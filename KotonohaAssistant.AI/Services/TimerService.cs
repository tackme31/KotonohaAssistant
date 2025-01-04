using KotonohaAssistant.Core.Utils;
using NAudio.Wave;

namespace KotonohaAssistant.AI.Services;

public interface ITimerService
{
    void SetTimer(int seconds);

    void StopAllTimers();
}

public class TimerService(ILogger logger) : ITimerService
{
    /// <summary>
    /// タスクとキャンセルトークンを格納するリスト
    /// </summary>
    private readonly List<(Task Task, CancellationTokenSource Cts)> _timers = [];

    private static readonly string AudioFilePath = @"D:\Windows\Programs\csharp\KotonohaAssistant\assets\Clock-Alarm02-1(Loop).mp3";

    private readonly ILogger _logger = logger;

    public void SetTimer(int seconds)
    {
        var cancellationTokenSource = new CancellationTokenSource();

        // 非同期タスクを開始
        var task = Task.Run(async () =>
        {
            using var audioFile = new AudioFileReader(AudioFilePath);
            using var outputDevice = new WaveOutEvent();
            outputDevice.Init(audioFile);

            var completed = false;

            outputDevice.PlaybackStopped += (sender, e) =>
            {
                if (!cancellationTokenSource.Token.IsCancellationRequested && !completed)
                {
                    audioFile.Position = 0; // 先頭に戻す
                    outputDevice.Play();
                }
            };

            try
            {
                // 指定時間経過後に再生
                await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationTokenSource.Token);
                outputDevice.Play();

                // 30秒後に強制的に終了
                var end = DateTime.Now + TimeSpan.FromSeconds(30);

                // maxTimeの間、音を鳴らし続ける
                while (DateTime.Now < end && !cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(100); // 100msごとにチェックしてキャンセルを検出
                }

                // 音声停止
                completed = true;
                outputDevice.Stop();
            }
            catch (OperationCanceledException)
            {
                // キャンセルされた場合は停止
                completed = true;
                outputDevice.Stop();

                _logger.LogInformation("タイマーを停止しました");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
            }
            finally
            {
                _timers.RemoveAll(t => t.Task.Id == Task.CurrentId);
            }
        });

        // タイマー情報をリストに保存
        _timers.Add((task, cancellationTokenSource));
    }

    // すべてのタイマーをキャンセルするメソッド
    public void StopAllTimers()
    {
        // すべてのキャンセルトークンをキャンセル
        foreach (var (_, cancellationTokenSource) in _timers)
        {
            cancellationTokenSource.Cancel();
        }

        // タイマーリストをクリア
        _timers.Clear();
    }
}
