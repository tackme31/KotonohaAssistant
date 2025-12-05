using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class StopAlarm(IPromptRepository promptRepository, ILogger logger) : ToolFunction(logger)
{
    public override string Description => promptRepository.StopAlarmDescription;

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

    public override async Task<string> Invoke(IDictionary<string, object> arguments, IReadOnlyConversationState state)
    {
        try
        {
            using var alarmClient = new AlarmClient();
            await alarmClient.StopAlarm();

            return "アラームを停止しました";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            return "アラームの停止に失敗しました";
        }
    }
}