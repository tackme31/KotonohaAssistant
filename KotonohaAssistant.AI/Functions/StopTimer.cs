using KotonohaAssistant.AI.Services;
using KotonohaAssistant.AI.Utils;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class StopTimer(ITimerService timerRepository) : ToolFunction
{
    public override string Description => """
この関数は、タイマーの停止を依頼されたときに呼び出されます。タイマー停止の依頼があった際に実行されます。

## 呼び出される例

- 「タイマー停止」
- 「タイマー止めて」

## 注意点

1. **発言制限:**  
   タイマーを停止したこと以外は発言しないでください。
""";

    public override string Parameters => """
{
    "type": "object",
    "properties": {},
    "required": [],
    "additionalProperties": false
}
""";

    private readonly ITimerService _timerRepository = timerRepository;

    public override bool CanBeLazy => false;

    public override bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments)
    {
        arguments = new Dictionary<string, object>();
        return true;
    }

    public override Task<string> Invoke(IDictionary<string, object> arguments, IReadOnlyConversationState state)
    {
        _timerRepository.StopAllTimers();

        return Task.FromResult("タイマーを停止しました。");
    }
}