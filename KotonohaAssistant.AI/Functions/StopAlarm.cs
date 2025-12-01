using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class StopAlarm(IAlarmService service, ILogger logger) : ToolFunction(logger)
{
    public override string Description => """
この関数は、再生中のアラームを停止するために呼び出されます。アラーム停止の依頼があった際に実行されます。

## 呼び出される例

- 「アラーム停止」
- 「アラームを止めてくれない？」

## 呼び出し後のセリフ
- タイマーを停止したことを**一言で**伝えてください。
- 「おつかれさま」「どうだった？」などのアラームの停止とは無関係な発言はしないでください。
""";

    public override string Parameters => """
{
    "type": "object",
    "properties": {},
    "required": [],
    "additionalProperties": false
}
""";

    private readonly IAlarmService _alarmService = service;

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
            _alarmService.StopAlarm();

            return Task.FromResult("アラームを停止しました");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            return Task.FromResult("アラームの停止に失敗しました");
        }
    }
}