using System.ComponentModel;
using System.Text.Json;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.AI.Functions;

public class StartTimer(IPromptRepository promptRepository, IAlarmClient alarmClient, ILogger logger)
    : ToolFunction(logger)
{
    private record Parameters(
        [property: Description("タイマーの秒数")]
        int Seconds);

    public override string Description => promptRepository.StartTimerDescription;

    protected override Type ParameterType => typeof(Parameters);

    public override bool CanBeLazy => false;

    public override async Task<string?> Invoke(JsonDocument argumentsDoc, IReadOnlyConversationState state)
    {
        var args = Deserialize<Parameters>(argumentsDoc);
        if (args == null)
        {
            return null;
        }

        try
        {
            await alarmClient.StartTimer(TimeSpan.FromSeconds(args.Seconds));

            var time = new TimeSpan(0, 0, args.Seconds);
            if (time.TotalSeconds < 60)
            {
                return $"タイマーを開始しました。: {time.Seconds}秒";
            }
            else
            {
                return $"タイマーを開始しました。: {time.Minutes}分{time.Seconds}秒";
            }

        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            return "タイマーの設定に失敗しました。";
        }
    }
}
