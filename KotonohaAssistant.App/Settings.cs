using KotonohaAssistant.AI.Functions;

namespace KotonohaAssistant.App;

public class Settings
{
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
