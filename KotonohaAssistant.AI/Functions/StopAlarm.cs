using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.Core.Utils;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class StopAlarm : ToolFunction
{
    public override string Description => """
再生中のアラームを停止します。
アラームの停止を依頼されたときに呼び出されます。

呼び出される例: 「タイマー止めて」
""";

    public override string Parameters => """
{
    "type": "object",
    "properties": {},
    "required": [],
    "additionalProperties": false
}
""";

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

    public override async Task<string> Invoke(IDictionary<string, object> arguments)
    {
        using var client = new VoiceClient();

        try
        {
            await client.StopAsync();

            return "OK";
        }
        catch (Exception)
        {
            return "ERROR";
        }
    }
}