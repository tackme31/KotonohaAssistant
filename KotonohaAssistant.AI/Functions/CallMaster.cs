using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Utils;
using KotonohaAssistant.Alarm;
using OpenAI.Chat;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class CallMaster(IAlarmRepository alarmRepository, IChatCompletionRepository chatCompletionRepository) : ToolFunction
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
""";

    public override string Parameters => """
{
    "type": "object",
    "properties": {
        "time": {
            "type": "string",
            "description": "設定時間。フォーマットはHH:mm"
        }
    },
    "required": [ "time" ],
    "additionalProperties": false
}
""";

    private readonly IAlarmRepository _alarmRepository = alarmRepository;
    private readonly IChatCompletionRepository _chatCompletionRepository = chatCompletionRepository;

    public override bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments)
    {
        arguments = new Dictionary<string, object>();

        var time = doc.RootElement.GetTimeSpanProperty("time");
        if (time is null)
        {
            return false;
        }

        arguments["time"] = time;

        return true;
    }

    public override async Task<string> Invoke(IDictionary<string, object> arguments, IReadOnlyConversationState state)
    {
        // 直近の2回のやり取りを取得する
        var messages = new List<ChatMessage>();
        var maxRefCount = 10;
        var refCount = 0;
        foreach (var message in state.ChatMessages.Reverse().SkipWhile(m => m is not UserChatMessage))
        {
            messages.Insert(0, message);

            if (message is not UserChatMessage user || user.Content is [])
            {
                continue;
            }

            if (user.Content[0].Text.StartsWith("私:"))
            {
                refCount++;
            }

            if (refCount >= maxRefCount)
            {
                break;
            }
        }

        var systemMessage = state.CurrentSister switch
        {
            Core.Kotonoha.Akane => Prompts.Alarm.CreateAlarmMessageAkane(DateTime.Now),
            Core.Kotonoha.Aoi => Prompts.Alarm.CreateAlarmMessageAoi(DateTime.Now),
            _ => Prompts.Alarm.CreateAlarmMessageAkane(DateTime.Now),
        };
        messages.Insert(0, new SystemChatMessage(systemMessage));
        messages.Add(new UserChatMessage(Prompts.Alarm.HintMessage));

        var voiceText = string.Empty;
        try
        {
            var result = await _chatCompletionRepository.CompleteChatAsync(messages);
            if (!result.Value.Content.Any())
            {
                throw new Exception("");
            }

            var completion = result.Value.Content[0].Text;

            voiceText = completion.Replace("ボイステキスト: ", string.Empty);
        }
        catch(Exception)
        {
            // TODO: ログ出力
        }

        if (string.IsNullOrWhiteSpace(voiceText))
        {
            return "FAILED";
        }

        var time = (TimeSpan)arguments["time"];
        var setting = new AlarmSetting
        {
            TimeInSeconds = time.TotalSeconds,
            Sister = state.CurrentSister,
            Message = voiceText
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