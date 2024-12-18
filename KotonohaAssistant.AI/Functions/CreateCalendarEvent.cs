using KotonohaAssistant.AI.Extensions;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class CreateCalendarEvent : ToolFunction
{
    public override string Description => """
予定の作成を依頼されたときに呼び出されます。
予定の作成に成功した場合はokを返し、失敗した場合はngを返します。

呼び出される例:「明日の15時に買い物の予定作って」「金曜日に通院の予定入れといて」

タイトル、日時が不明な場合は、呼び出さず、聞き返してください。
""";

    public override string Parameters => """
{
    "type": "object",
    "properties": {
        "title": {
            "type": "string",
            "description": "作成する予定のタイトル"
        },
        "date": {
            "type": "string",
            "description": "予定の日にち。yyyy/MM/dd形式"
        },
        "time": {
            "type": "string",
            "description": "予定の時間。HH:mm形式。不明な場合はnull"
        }
    },
    "required": [ "title", "date" ],
    "additionalProperties": false
}
""";

    public override bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments)
    {
        arguments = new Dictionary<string, object>();

        var title = doc.RootElement.GetStringProperty("title");
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }
        arguments["title"] = title;

        var date = doc.RootElement.GetDateTimeProperty("date");
        if (date is null)
        {
            return false;
        }
        arguments["date"] = date;

        var time = doc.RootElement.GetTimeSpanProperty("time");
        if (time is not null)
        {
            arguments["time"] = time;
        }

        return true;
    }

    public override string Invoke(IDictionary<string, object> arguments)
    {
        Console.WriteLine($"  => {GetType().Name}({string.Join(", ", arguments.Select((p) => $"{p.Key}={p.Value}"))})");

        return "ok";
    }
}