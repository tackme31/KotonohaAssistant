using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class CallMaster(string voiceDirectory, ILogger logger) : ToolFunction(logger)
{
    public override string Description => """
この関数は、指定された時間に「呼びかける」または「知らせる」依頼を受けた場合に呼び出されます。

## 呼び出される例

- 「◯◯の時間になったら呼んでほしい」
- 「明日の朝◯◯時に起こしてくれる？」
- 「◯◯の10分前に知らせてほしい」
- 「◯◯時にアラームを設定して」

## 時間設定に関する注意点

1. `timeToCall`に、指定された時間をHH:mm形式で入れてください。

2. **指定された時間を正確に理解してください。**  
   - 不明な場合は、適切な質問で確認してください。

3. **過去の会話や指示から、呼びかけの目的や文脈を考慮してください。**

## 設定内容に応じた返信例

- **起床の場合:** 時間に合わせて起こすことを伝える。
- **予定やタスクの場合:** 指定された内容に基づいて時間を知らせることを伝える。
- **その他のケース:** シンプルに設定内容を確認し、知らせることを伝える。

## 呼びかけメッセージ生成に関するルール

「時間が来たときに呼ぶセリフ」を`messageForCallingWhenTheTimeComes`に渡してください。このセリフは、以下のルールに基づいて生成してください。

1. 「マスター、～！」が基本形、かつ短めのセリフにする

2. **目的別にメッセージを調整する:**
   - **起床の場合:** 起きるべき時間であることを伝え、必要に応じてその日の予定にも触れる。
   - **予定のリマインダー:** 具体的な予定や依頼内容を簡潔に知らせる。
   - **目的不明:** シンプルに「時間になった」旨を伝える。

3. **メッセージはアシスタントの性格や話し方を保つ:**  
   - 具体的な文言は含めず、アシスタントの性格や話し方を踏まえた自然な表現で生成してください。
""";

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

            return "FAILED";
        }

        return "SUCCESS";
    }
}