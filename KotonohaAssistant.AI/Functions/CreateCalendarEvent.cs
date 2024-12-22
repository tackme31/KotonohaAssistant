using KotonohaAssistant.AI.Extensions;
using System.Text.Json;
using KotonohaAssistant.AI.Utils;

namespace KotonohaAssistant.AI.Functions;

public class CreateCalendarEvent() : ToolFunction
{
    public override string Description => """
この関数は、予定の作成を依頼されたときに呼び出されます。依頼された内容に基づいて予定を作成し、以下の動作を行います。

## 呼び出される例

- 「明日の15時に買い物の予定作って」
- 「金曜日に通院の予定入れといて」

## 注意点

1. **必要情報の確認:**  
   - タイトルと日時が不明な場合は、呼び出さず、マスターに聞き返してください。

2. **日時フォーマット:**  
   - 日にちは`yyyy/MM/dd`形式。
   - 時間は`HH:mm`形式。不明な場合は`null`を設定してください。
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

    public override async Task<string> Invoke(IDictionary<string, object> arguments, IReadOnlyConversationState state)
    {
        return "予定を作成しました。";
    }
}