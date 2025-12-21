using System.Text.Json;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.AI.Functions;

public class StopAlarm(IPromptRepository promptRepository, IAlarmClient alarmClient, ILogger logger)
    : ToolFunction(logger)
{
    private record Parameters();

    public override string Description => promptRepository.StopAlarmDescription;

    protected override Type ParameterType => typeof(Parameters);

    public override bool CanBeLazy => false;

    public override async Task<string?> Invoke(JsonDocument argumentsDoc, IReadOnlyConversationState state)
    {
        try
        {
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
