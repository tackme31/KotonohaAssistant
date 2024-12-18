using KotonohaAssistant.AI.Extensions;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class WillCall : ToolFunction
{
    public override string Description => """
指定された時間になったら知らせることを覚えます（内部的には、アラームを設定します）。

呼び出される例:
「10時にアラームを設定して」
「明日の8時に起こしてほしい」
「{予定}の時間になったら呼んでくれる？」

時間がわからない、あるいは過去の会話から推測できない場合は、聞き返してください。
設定に成功したら、呼び出し時の指示に応じて、以下の例ように返信内容をしてください。

- アラームの設定: アラームを設定した旨の返事
- 呼んでほしい、知らせてほしい: 時間になったら呼ぶ、知らせる旨の返信
- 起こしてほしい: 時間になったら起こす旨の返信
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

    public override string Invoke(IDictionary<string, object> arguments)
    {
        Console.WriteLine($"  => {GetType().Name}({string.Join(", ", arguments.Select((p) => $"{p.Key}={p.Value}"))})");

        return "SUCCESS";
    }
}