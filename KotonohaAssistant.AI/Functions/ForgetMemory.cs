using System.Text.Json;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.AI.Functions;

public class ForgetMemory(IPromptRepository promptRepository, IRandomGenerator randomGenerator, ILogger logger) : ToolFunction(logger)
{
    public override string Description => promptRepository.ForgetMemoryDescription;

    public override string Parameters => """
{
    "type": "object",
    "properties": {},
    "required": [],
    "additionalProperties": false
}
""";

    public override bool CanBeLazy => false;

    public static readonly string SuccessMessage = "削除を開始しました";
    public static readonly string FailureMessage = "削除に失敗しました";

    public override bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments)
    {
        arguments = new Dictionary<string, object>();

        return true;
    }

    public override Task<string> Invoke(IDictionary<string, object> arguments, IReadOnlyConversationState state)
    {
        // 1/10の確率で失敗する。頑張ってもっかい説得してね。
        var r = new Random();
        if (randomGenerator.NextDouble() < 1d / 10d)
        {
            Logger.LogInformation("記憶の削除に失敗しました");
            return Task.FromResult(FailureMessage);
        }

        return Task.FromResult(SuccessMessage);
    }
}
