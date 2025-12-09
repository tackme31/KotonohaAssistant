using System;
using System.Threading.Tasks;

namespace KotonohaAssistant.Core.Utils;

/// <summary>
/// AlarmAppクライアントの抽象化インターフェース（テスト可能性のため）
/// </summary>
public interface IAlarmClient : IDisposable
{
    /// <summary>
    /// アラームを追加します
    /// </summary>
    Task AddAlarm(TimeSpan time, string voicePath, bool isRepeated);

    /// <summary>
    /// アラームを停止します
    /// </summary>
    Task StopAlarm();

    /// <summary>
    /// タイマーを開始します
    /// </summary>
    Task StartTimer(TimeSpan time);

    /// <summary>
    /// タイマーを停止します
    /// </summary>
    Task StopTimer();
}
