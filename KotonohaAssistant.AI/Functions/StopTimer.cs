using System.Text.Json;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.AI.Functions;

public class StopTimer(IPromptRepository promptRepository, IAlarmClient alarmClient, ILogger logger) : ToolFunction(logger)
{
    private record Parameters();

    public override string Description => promptRepository.StopTimerDescription;

    protected override Type ParameterType => typeof(Parameters);

    public override bool CanBeLazy => false;

    public override async Task<string?> Invoke(JsonDocument argumentsDoc, IReadOnlyConversationState state)
    {
        try
        {
            await alarmClient.StopTimer();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            return "タイマーの停止に失敗しました。";
        }

        return "タイマーを停止しました";
    }
}
