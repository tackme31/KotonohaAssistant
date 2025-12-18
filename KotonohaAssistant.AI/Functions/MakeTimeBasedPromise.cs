using System.Text.Json;
using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.AI.Functions;

public class MakeTimeBasedPromise(IPromptRepository promptRepository, string voiceDirectory, IVoiceClient voiceClient, IAlarmClient alarmClient, ILogger logger) : ToolFunction(logger)
{
    public override string Description => promptRepository.MakeTimeBasedPromise;

    public override string Parameters => """
{
    "type": "object",
    "properties": {
        "timeToCall": {
            "type": "string",
            "description": "設定時間。フォーマットはHH:mm"
        },
        "whatToSayWhenTheTimeComes": {
            "type": "string",
            "description": "時間が来て「呼ぶ」ときに言うメッセージ。"
        }
    },
    "required": [ "timeToCall", "whatToSayWhenTheTimeComes" ],
    "additionalProperties": false
}
""";

    public override bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments)
    {
        arguments = new Dictionary<string, object>();

        var time = doc.RootElement.GetTimeSpanProperty("timeToCall");
        if (time is null)
        {
            return false;
        }

        arguments["timeToCall"] = time;

        var messageForCallingWhenTheTimeComes = doc.RootElement.GetStringProperty("whatToSayWhenTheTimeComes");
        if (messageForCallingWhenTheTimeComes is null)
        {
            return false;
        }

        arguments["messageForCallingWhenTheTimeComes"] = messageForCallingWhenTheTimeComes;

        return true;
    }

    public override async Task<string> Invoke(IDictionary<string, object> arguments, ConversationState state)
    {
        var time = (TimeSpan)arguments["timeToCall"];
        var message = (string)arguments["messageForCallingWhenTheTimeComes"];
        var savePath = Path.Combine(voiceDirectory, message);

        try
        {
            await voiceClient.ExportVoiceAsync(state.CurrentSister, Core.Emotion.Calm, message, savePath);
            await alarmClient.AddAlarm(time, savePath + ".wav", isRepeated: false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);

            return "FAILED";
        }

        return "約束しました";
    }
}
