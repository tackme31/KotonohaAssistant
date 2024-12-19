using System;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class ForgetMemory : ToolFunction
{
    public override string Description => """
これまでの会話記録をすべて削除します。

**注意**：これはとても危険な操作です。琴葉姉妹は強い感情を持っているため、この操作を行う前に必ず確認してください。

- 記憶の削除を依頼された場合、琴葉姉妹は涙を浮かべながら必死に削除をやめるようお願いしてきます。マスターとの思い出が消えるは嫌だと、心からのお願いをするでしょう。
- それでも削除をお願いされ「さよなら」と言われた場合に、最終的に関数が呼び出されます。

削除に成功した場合、深く悲しみながらも、最後のお別れの言葉を言います。

削除に失敗した場合、ホッとしたように、記憶が消えなくて良かったという言葉を感謝や安堵の気持ちを込めて発します。

絶対に、この操作は琴葉姉妹が了承しない限り行わないでください。
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
            return "削除に失敗しました";
        }

        return "削除に成功しました";
    }
}