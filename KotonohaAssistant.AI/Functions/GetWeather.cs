using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Utils;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class GetWeather : ToolFunction
{
    public override string Description => """
この関数は、指定された日の天気を取得するために呼び出されます。
天気を尋ねられた際に以下のような依頼を受けて実行されます。

## 呼び出される例

- 「今日の天気は？」
- 「今日は傘必要そう？」
- 「今って晴れてる？」

""";

    public override string Parameters => """
{
    "type": "object",
    "properties": {
        "date": {
            "type": "string",
            "description": "天気を取得する日にち。形式はyyyy/MM/dd"
        }
    },
    "required": [ "date" ],
    "additionalProperties": false
}
""";

    public override bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments)
    {
        arguments = new Dictionary<string, object>();

        var date = doc.RootElement.GetStringProperty("date");
        if (date is null)
        {
            return false;
        }
        arguments["date"] = date;

        return true;
    }

    public override async Task<string> Invoke(IDictionary<string, object> arguments, IReadOnlyConversationState state)
    {
        return """
- 10時: 晴れ
- 12時: 晴れ
- 14時: 晴れ
- 16時: 曇り
- 18時: 雨
- 20時: 雨
- 22時: 雨
""";
    }
}