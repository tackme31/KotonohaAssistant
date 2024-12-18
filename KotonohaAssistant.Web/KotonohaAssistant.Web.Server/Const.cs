using KotonohaAssistant.AI.Functions;

namespace KotonohaAssistant.Web.Server;

public class Const
{
    /// <summary>
    /// 利用可能なツール関数リスト
    /// </summary>
    public static readonly List<ToolFunction> Functions =
    [
        new SetAlarm(),
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
    public static readonly List<string> ExcludeFunctionNamesFromLazyMode =
    [
        nameof(StartTimer),
        nameof(ForgetMemory),
    ];

    public static readonly string ModelName = "gpt-4o-mini";
}
