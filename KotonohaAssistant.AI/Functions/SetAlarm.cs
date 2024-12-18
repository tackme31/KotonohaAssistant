using KotonohaAssistant.AI.Extensions;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class SetAlarm : ToolFunction
{
    public override string Description => """
アラームの設定を依頼されたときに呼び出されます。
アラームの設定に成功した場合はokを返し、失敗した場合はngを返します。

呼び出される例: 「10時にアラームを設定して」「明日の8時に起こしてほしい」「{予定}の時間になったら呼んでくれる？」

時間がわからない、あるいは過去の会話から推測できない場合は、聞き返してください。
また「少し前」などの表現があった場合は、10分前のことだと解釈してください。
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

        return "ok";
    }
}