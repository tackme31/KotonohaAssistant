using System.Text.Json;
using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.AI.Functions;

public class StartTimer(IPromptRepository promptRepository, ILogger logger) : ToolFunction(logger)
{
    public override string Description => promptRepository.StartTimerDescription;

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

    public override bool CanBeLazy => false;

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

        try
        {
            using var client = new AlarmClient();

            await client.StartTimer(TimeSpan.FromSeconds(seconds));

            return "成功";
        }
        catch (Exception ex)
        {
            logger.LogError(ex);
            return "失敗";
        }
    }
}
