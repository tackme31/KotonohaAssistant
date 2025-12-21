using System.ComponentModel;
using System.Text.Json;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.AI.Functions;

public class MakeTimeBasedPromise
    (IPromptRepository promptRepository, string voiceDirectory, IVoiceClient voiceClient, IAlarmClient alarmClient, ILogger logger)
    : ToolFunction(logger)
{
    private record Parameters(
        [property: Description("設定時間。フォーマットはHH:mm")]
        string TimeToCall,
        [property: Description("時間が来て「呼ぶ」ときに言うメッセージ。")]
        string WhatToSayWhenTheTimeComes);

    public override string Description => promptRepository.MakeTimeBasedPromise;

    protected override Type ParameterType => typeof(Parameters);

    protected override bool ValidateParameters<T>(T parameters)
    {
        if (parameters is not Parameters args)
        {
            return false;
        }

        if (!TimeSpan.TryParse(args.TimeToCall, out _))
        {
            Logger.LogWarning($"Invalid time format: {args.TimeToCall}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(args.WhatToSayWhenTheTimeComes))
        {
            Logger.LogWarning($"WhatToSayWhenTheTimeComes is required.");
            return false;
        }

        return true;
    }

    public override async Task<string?> Invoke(JsonDocument argumentsDoc, IReadOnlyConversationState state)
    {
        var args = Deserialize<Parameters>(argumentsDoc);
        if (args is null)
        {
            return null;
        }

        var time = TimeSpan.Parse(args.TimeToCall);
        var savePath = Path.Combine(voiceDirectory, args.WhatToSayWhenTheTimeComes);
        try
        {
            await voiceClient.ExportVoiceAsync(state.CurrentSister, Core.Emotion.Calm, args.WhatToSayWhenTheTimeComes, savePath);
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
