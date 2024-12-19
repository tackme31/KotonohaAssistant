using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.Core;

namespace KotonohaAssistant.App;

public class Settings
{
    /// <summary>
    /// ウェイクワード
    /// </summary>
    public static readonly string[] WakeWords = [
        "ねえあおいちゃん",
        "ねえ葵ちゃん",
        "ねあおいちゃん",
        "ね葵ちゃん",
        "あおいちゃんいる",
        "葵ちゃんいる",
        "ねえあかねちゃん",
        "ねえ茜ちゃん",
        "ねあかねちゃん",
        "ね茜ちゃん",
        "茜ちゃんいる",
        "あかねちゃんいる",
    ];

    /// <summary>
    /// デフォルトの会話対象
    /// </summary>
    public static readonly Kotonoha DefaultSister = Kotonoha.Akane;

    /// <summary>
    /// 茜ちゃんの性格（振る舞い）
    /// </summary>
    public static readonly string? AkaneBehaviour = Behaviour.Default;

    /// <summary>
    /// 葵ちゃんの性格（振る舞い）
    /// </summary>
    public static readonly string? AoiBehaviour = Behaviour.Default;

    /// <summary>
    /// 利用可能なツール関数リスト
    /// </summary>
    public static readonly List<ToolFunction> Functions = [
        new CallMaster(),
        new StartTimer(),
        new CreateCalendarEvent(),
        new GetCalendarEvent(),
        new GetWeather(),
        new TurnOnHeater(),
        new ForgetMemory(),
    ];

    /// <summary>
    /// 怠けモードで除外される関数名
    /// </summary>
    public static readonly List<string> ExcludeFunctionNamesFromLazyMode = [
        nameof(StartTimer),
        nameof(ForgetMemory),
    ];

    public static readonly string ModelName = "gpt-4o-mini";
}
