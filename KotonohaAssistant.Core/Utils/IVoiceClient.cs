using System;
using System.Threading.Tasks;
using KotonohaAssistant.Core.Models;

namespace KotonohaAssistant.Core.Utils;

/// <summary>
/// VoiceServerクライアントの抽象化インターフェース（テスト可能性のため）
/// </summary>
public interface IVoiceClient : IDisposable
{
    /// <summary>
    /// 音声を合成して再生します
    /// </summary>
    Task SpeakAsync(Kotonoha sister, Emotion emotion, string message);

    /// <summary>
    /// 再生中の音声を停止します
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 音声を合成してファイルにエクスポートします
    /// </summary>
    Task ExportVoiceAsync(Kotonoha sister, Emotion emotion, string message, string path);
}
