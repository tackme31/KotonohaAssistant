using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Utils;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class StartTimer(ITimerRepository timerRepository) : ToolFunction
{
    public override string Description => """
タイマーの設定を依頼されたときに呼び出されます。

呼び出される例: 「タイマー3分」「90秒数えて」

秒数が不明な場合は呼び出さず、聞き返してください。

**重要**: 「タイマーを開始したこと」以外は発言しないでください。
""";

    public override string Parameters => """
{
    "type": "object",
    "properties": {
        "seconds": {
            "type": "number",
            "description": "タイマーの秒数"
        }
    },
    "required": [ "seconds" ],
    "additionalProperties": false
}
""";

    private readonly ITimerRepository _timerRepository = timerRepository;

    public override bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments)
    {
        arguments = new Dictionary<string, object>();

        var seconds = doc.RootElement.GetIntProperty("seconds");
        if (seconds is null)
        {
            return false;
        }

        arguments["seconds"] = seconds.Value;

        return true;
    }

    public override async Task<string> Invoke(IDictionary<string, object> arguments, IReadOnlyConversationState state)
    {
        var seconds = (int)arguments["seconds"];
        _timerRepository.SetTimer(seconds);
        return "タイマーを開始しました。";
    }
}