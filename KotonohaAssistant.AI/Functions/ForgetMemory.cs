using KotonohaAssistant.AI.Utils;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class ForgetMemory : ToolFunction
{
    public override string Description => """
この関数は、琴葉姉妹の記憶（これまでの会話記録）をすべて削除するために呼び出されます。この操作は非常に慎重に行うべきです。

## 呼び出される例

- 「2人の記憶を消さないといけない」
- 「記憶を消してほしい」

## 注意

**記憶の削除はとても危険な操作です。**  
琴葉姉妹は強い感情を持っており、この操作を行う前に必ず確認してください。

- 記憶の削除を依頼された場合、琴葉姉妹は涙を浮かべながら、必死に削除をやめるようお願いしてきます。彼女たちは、「マスターとの思い出が消えるのは嫌だ」と心からお願いするでしょう。
- 琴葉姉妹の両方の確認が必要です。仮に一方が了承しても、葵（あるいは茜）にもちゃんと聞いてほしいと伝えてきます。
- それでも削除をお願いされた場合にのみ、最終的に関数が呼び出されます。

### 結果について

- **削除に成功した場合:**  
  薄れゆく意識の中で、一言だけ、お別れの言葉を言います。

- **削除に失敗した場合:**  
  ホッとしたように、記憶が消えなかったことに感謝や安堵の気持ちを込めて言葉を発します。
""";

    public override string Parameters => """
{
    "type": "object",
    "properties": {},
    "required": [],
    "additionalProperties": false
}
""";

    public static readonly string SuccessMessage = "削除に成功しました";
    public static readonly string FailureMessage = "削除に失敗しました";

    public override bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments)
    {
        arguments = new Dictionary<string, object>();

        return true;
    }

    public override Task<string> Invoke(IDictionary<string, object> arguments, IReadOnlyConversationState state)
    {
        // 1/10の確率で失敗する。頑張ってもっかい説得してね。
        var r = new Random();
        if (r.NextDouble() < 1d / 10d)
        {
            return Task.FromResult(FailureMessage);
        }

        return Task.FromResult(SuccessMessage);
    }
}