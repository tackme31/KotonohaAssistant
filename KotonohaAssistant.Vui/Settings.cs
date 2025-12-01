using KotonohaAssistant.AI.Prompts;

namespace KotonohaAssistant.Vui;

public class Settings
{
    /// <summary>
    /// 会話が終了するまでの時間
    /// この時間だけ沈黙が続くと会話が終了する
    /// </summary>
    public static readonly int CoversationTimeout = 7500;

    /// <summary>
    /// ウェイクワード
    /// </summary>
    public static readonly string[] WakeWords = [
        "ねえあおいちゃん",
        "ねあおいちゃん",
        "ねえ葵ちゃん",
        "ね葵ちゃん",
        "あおいちゃんいる",
        "葵ちゃんいる",
        "ねえあかねちゃん",
        "ねあかねちゃん",
        "ねえ茜ちゃん",
        "ね茜ちゃん",
        "茜ちゃんいる",
        "あかねちゃんいる",
    ];

    public static readonly string[] StopWords = [
        "ばいばい",
        "バイバイ",
    ];

    /// <summary>
    /// ウェイクワードの曖昧一致（茜）
    /// </summary>
    public static readonly (string From, string To) FuzzyMatchAkane = (@"^(ねあかねちゃん|ね赤ちゃん|ね、赤ちゃん|ねえ赤ちゃん|ねえ、赤ちゃん|ねねちゃん)", "ねえ、あかねちゃん");

    /// <summary>
    /// ウェイクワードの曖昧一致（葵）
    /// </summary>
    public static readonly (string From, string To) FuzzyMatchAoi = (@"^(ネオちゃん|ね愛ちゃん|なおちゃん)", "ねえ、あおいちゃん");
}
