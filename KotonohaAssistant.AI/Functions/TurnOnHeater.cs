using KotonohaAssistant.AI.Extensions;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class TurnOnHeater : ToolFunction
{
    public override string Description => """
暖房の設定を依頼されたときに呼び出されます。
暖房の設定に成功した場合はokを返し、失敗した場合はngを返します。

呼び出される:「18時ごろに部屋温めておいて」「朝7時に暖房設定しておいて」

時間が不明な場合は、呼び出さず、聞き返してください。
""";

    public override string Parameters => """
{
    "type": "object",
    "properties": {
        "time": {
            "type": "string",
            "description": "暖房の設定時間。HH:mm形式"
        }
    },
    "required": [ "time" ],
    "additionalProperties": false
}
""";

    public override bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments)
    {
        arguments = new Dictionary<string, object>();

        var time = doc.RootElement.GetTimeSpanProperty("time");
        if (time is null)
        {
            return false;
        }

        arguments["time"] = time;

        return true;
    }

    public override string Invoke(IDictionary<string, object> arguments)
    {
        Console.WriteLine($"  => {GetType().Name}({string.Join(", ", arguments.Select((p) => $"{p.Key}={p.Value}"))})");

        return "ok";
    }
}