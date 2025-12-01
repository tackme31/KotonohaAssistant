using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class StopTimer(ITimerService timerRepository, ILogger logger) : ToolFunction(logger)
{
    public override string Description => """
この関数は、タイマーの停止を依頼されたときに呼び出されます。タイマー停止の依頼があった際に実行されます。

## 呼び出される例

- 「タイマー停止」
- 「タイマー止めて」

## 呼び出し後のセリフ
- タイマーを停止したことを**一言で**伝えてください。
- 「おつかれさま」「どうだった？」などのタイマーの停止とは無関係な発言はしないでください。
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
        try
        {
            _timerRepository.StopAllTimers();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            return Task.FromResult("タイマーの停止に失敗しました。");
        }

        return Task.FromResult("タイマーを停止しました。");
    }
}