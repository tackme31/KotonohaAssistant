using System.Text.Json;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.AI.Functions;

public class StopTimer(IPromptRepository promptRepository, IAlarmClient alarmClient, ILogger logger) : ToolFunction(logger)
{
    public override string Description => promptRepository.StopTimerDescription;

    public override string Parameters => """
{
    "type": "object",
    "properties": {},
    "required": [],
    "additionalProperties": false
}
""";

    public override bool CanBeLazy => false;

    public override bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments)
    {
        arguments = new Dictionary<string, object>();
        return true;
    }

    public override async Task<string> Invoke(IDictionary<string, object> arguments, ConversationState_ state)
    {
        try
        {
            await alarmClient.StopTimer();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            return "タイマーの設定に失敗しました。";
        }

        return "タイマーを設定しました";
    }
}
