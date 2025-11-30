using KotonohaAssistant.AI.Utils;
using KotonohaAssistant.Core.Utils;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class GetCurrentTime(ILogger logger) : ToolFunction(logger)
{
    public override string Description => """
この関数は、返信の生成に時間を必要とする場合に呼び出されます。

## 呼び出される例

- 「今何時？」
- 「今日の天気は？」
- 「明後日の予定を教えて」

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

    public override async Task<string> Invoke(IDictionary<string, object> arguments, IReadOnlyConversationState state)
    {
        return await Task.FromResult(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
    }
}
