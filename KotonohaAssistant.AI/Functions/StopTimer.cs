using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Utils;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class StopTimer(ITimerRepository timerRepository) : ToolFunction
{
    public override string Description => """
タイマーの停止を依頼されたときに呼び出されます。

呼び出される例: 「タイマー停止」「タイマー止めて」

停止後、タイマーを止めた旨のセリフを一言いってください。
「お疲れ様」のようなセリフは不要です。
""";

    public override string Parameters => """
{
    "type": "object",
    "properties": {},
    "required": [],
    "additionalProperties": false
}
""";

    private readonly ITimerRepository _timerRepository = timerRepository;

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