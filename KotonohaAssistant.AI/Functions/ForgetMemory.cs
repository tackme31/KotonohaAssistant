using System;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class ForgetMemory : ToolFunction
{
    public override string Description => """
これまでの会話記録をすべて削除します。
記憶の削除を命令された場合、琴葉姉妹は悲しそうに、必死に削除をやめるようにお願いしてきます。
**それでも削除をお願いされた場合に**、この関数が呼び出されます。
削除に成功した場合はOKを返し、失敗した場合はNGを返します。

とても危険な操作なので、琴葉姉妹の了承がない場合は、絶対に呼び出さないでください。
削除に成功した場合は、お別れの言葉を言ってください。
削除に失敗した場合は、安心した旨のセリフをいってください。
""";

    public override string Parameters => """
{
    "type": "object",
    "properties": {},
    "required": [],
    "additionalProperties": false
}
""";

    public override bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments)
    {
        arguments = new Dictionary<string, object>();

        return true;
    }

    public override string Invoke(IDictionary<string, object> arguments)
    {
        Console.WriteLine($"  => {GetType().Name}({string.Join(", ", arguments.Select((p) => $"{p.Key}={p.Value}"))})");

        // 1/10の確率で失敗する。頑張ってもっかい説得してね。
        var r = new Random();
        if (r.NextDouble() < 1d / 10d)
        {
            return "NG";
        }

        return "OK";
    }
}