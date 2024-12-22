using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Utils;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class CallMaster(IAlarmRepository alarmRepository) : ToolFunction
{
    public override string Description => """
この関数は、指定された時間に呼びかけたり知らせたりする依頼を受けた場合に呼び出されます。
以下は、呼び出される例です。

- 「明日の8時に起こしてほしい」
- 「◯◯の時間になったら呼んでくれる？」
- 「10時にアラームを設定して」

もし過去の会話から時間を推測できない場合は、必ず聞き返してください。

また、呼び出し時の指示に応じて、以下の例ように返信してください。

- 起こしてほしい: 時間になったら起こす、という返事
- 呼んでほしい: 時間になったら呼ぶ、という返事
- その他: 時間になったら知らせる、という返事

さらに「時間が来たときに呼ぶセリフ」をmessageForCallingWhenTheTimeComesに渡してください。
直前の会話から、以下の中からいくつか選んで「マスター、～」のようなセリフを作ってください。

- 呼ぶ目的が過去の会話から推測できない場合: 時間になった、のようなシンプルなメッセージ。
- 予定リマインドの場合: 予定の時間になった旨のメッセージ。
- 起床の場合: マスターを起こすようなセリフ。もし起床後に予定があるなら、そのことも含めてください。

目的が不明な場合は、シンプルに時間になった旨のメッセージにしてください。

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

    private readonly IAlarmRepository _alarmRepository = alarmRepository;

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
        var setting = new AlarmSetting
        {
            TimeInSeconds = time.TotalSeconds,
            Sister = state.CurrentSister,
            Message = message
        };

        try
        {
            await _alarmRepository.InsertAlarmSetting(setting);
        }
        catch(Exception)
        {
            // TODO: ログ出力
        }

        return "SUCCESS";
    }
}