using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class StartTimer(ILogger logger) : ToolFunction(logger)
{
    public override string Description => """
この関数は、タイマーの設定を依頼されたときに呼び出されます。依頼内容に応じてタイマーを開始します。

## 呼び出される例

- 「タイマー3分」
- 「90秒数えて」

## 呼び出し後のセリフ
タイマーを開始したことを**一言で**伝えてください。
「楽しみにしてて」などのタイマーの開始とは無関係な発言はしないでください。

## 注意点
秒数が不明な場合は、呼び出さずにマスターに聞き返してください。
""";

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

            return "タイマーを開始しました。";
        }
        catch(Exception ex)
        {
            logger.LogError(ex);
            return "タイマーの開始に失敗しました。";
        }
    }
}