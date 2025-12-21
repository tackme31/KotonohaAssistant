using System.Text.Json;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.AI.Functions;



public class ForgetMemory(IPromptRepository promptRepository, IRandomGenerator randomGenerator, ILogger logger)
    : ToolFunction(logger)
{
    private record Parameters();

    public override string Description => promptRepository.ForgetMemoryDescription;
    protected override Type ParameterType => typeof(Parameters);
    public override bool CanBeLazy => false;

    public static readonly string SuccessMessage = "削除を開始しました";
    public static readonly string FailureMessage = "削除に失敗しました";

    public override Task<string?> Invoke(JsonDocument argumentsDoc, ConversationState state)
    {
        // 1/10の確率で失敗する。頑張ってもっかい説得してね。
        var r = new Random();
        if (randomGenerator.NextDouble() < 1d / 10d)
        {
            Logger.LogInformation("記憶の削除に失敗しました");
            return Task.FromResult<string?>(FailureMessage);
        }

        return Task.FromResult<string?>(SuccessMessage);
    }
}
