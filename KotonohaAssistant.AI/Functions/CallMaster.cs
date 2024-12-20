using KotonohaAssistant.AI.Extensions;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class CallMaster : ToolFunction
{
    public override string Description => """
この関数は、指定された時間に呼びかけたり知らせたりする依頼を受けた場合に呼び出されます。
以下は、呼び出される例です。

- 「明日の8時に起こしてほしい」
- 「◯◯の時間になったら呼んでくれる？」
- 「10時にアラームを設定して」

もし時間が不明確だったり、過去の会話から時間がわからない場合は、聞き返してください。

また、呼び出し時の指示に応じて、以下の例ように返信してください。

- 起こしてほしい: 時間になったら起こす、という返事
- 呼んでほしい: 時間になったら呼ぶ、という返事
- その他: 時間になったら知らせる、という返事
""";

    public override string Parameters => """
{
    "type": "object",
    "properties": {
        "time": {
            "type": "string",
            "description": "設定時間。フォーマットはHH:mm"
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

    public override async Task<string> Invoke(IDictionary<string, object> arguments)
    {
        Console.WriteLine($"  => {GetType().Name}({string.Join(", ", arguments.Select((p) => $"{p.Key}={p.Value}"))})");

        return "SUCCESS";
    }
}