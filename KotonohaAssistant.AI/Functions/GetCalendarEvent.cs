using KotonohaAssistant.AI.Extensions;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class GetCalendarEvent : ToolFunction
{
    public override string Description => """
指定された日の予定をGoogleカレンダーから取得します。予定を尋ねられたときに呼び出されます。

呼び出される例:「明日の予定教えて」「今日の15時からなにか予定あったっけ？」
""";

    public override string Parameters => """
{
    "type": "object",
    "properties": {
        "date": {
            "type": "string",
            "description": "予定を取得する日にち。形式はyyyy/MM/dd"
        }
    },
    "required": [ "date" ],
    "additionalProperties": false
}
""";

    public override bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments)
    {
        arguments = new Dictionary<string, object>();

        var date = doc.RootElement.GetDateTimeProperty("date");
        if (date is null)
        {
            return false;
        }
        arguments["date"] = date;

        return true;
    }

    public override string Invoke(IDictionary<string, object> arguments)
    {
        Console.WriteLine($"  => {GetType().Name}({string.Join(", ", arguments.Select((p) => $"{p.Key}={p.Value}"))})");

        return """
[終日] 原神アップデート日
[15:00 - 16:00] 通院
[18:00 - 20:00] Amazon荷物受け取り
""";
    }
}