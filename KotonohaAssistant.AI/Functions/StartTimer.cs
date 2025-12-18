using System.Text.Json;
using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.AI.Functions;

public class StartTimer(IPromptRepository promptRepository, IAlarmClient alarmClient, ILogger logger) : ToolFunction(logger)
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

    public override async Task<string> Invoke(IDictionary<string, object> arguments, ConversationState state)
    {
        var seconds = (int)arguments["seconds"];

        try
        {
            await alarmClient.StartTimer(TimeSpan.FromSeconds(seconds));

            var time = new TimeSpan(0, 0, seconds);
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
