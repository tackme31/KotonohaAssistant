using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class CallMaster(IPromptRepository promptRepository, string voiceDirectory, ILogger logger) : ToolFunction(logger)
{
    public override string Description => promptRepository.CallMasterDescription;

    public override string Parameters => """
{
    "type": "object",
    "properties": {
        "timeToCall": {
            "type": "string",
            "description": "設定時間。フォーマットはHH:mm"
        },
        "messageForCallingWhenTheTimeComes": {
            "type": "string",
            "description": "時間が来て「呼ぶ」ときに言うメッセージ。"
        }
    },
    "required": [ "timeToCall", "messageForCallingWhenTheTimeComes" ],
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

        var messageForCallingWhenTheTimeComes = doc.RootElement.GetStringProperty("messageForCallingWhenTheTimeComes");
        if (messageForCallingWhenTheTimeComes is null)
        {
            return false;
        }

        arguments["messageForCallingWhenTheTimeComes"] = messageForCallingWhenTheTimeComes;

        return true;
    }

    public override async Task<string> Invoke(IDictionary<string, object> arguments, IReadOnlyConversationState state)
    {
        var time = (TimeSpan)arguments["timeToCall"];
        var message = (string)arguments["messageForCallingWhenTheTimeComes"];
        var savePath = Path.Combine(voiceDirectory, message);

        try
        {
            using var voiceClient = new VoiceClient();
            using var alarmClient = new AlarmClient();

            await voiceClient.ExportVoiceAsync(state.CurrentSister, Core.Emotion.Calm, message, savePath);
            await alarmClient.AddAlarm(time, savePath + ".wav", isRepeated: false);
        }
        catch(Exception ex)
        {
            Logger.LogError(ex);

            return "ERROR";
        }

        return "OK";
    }
}