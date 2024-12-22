using KotonohaAssistant.AI.Utils;
using KotonohaAssistant.Alarm;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class StopAlarm(IAlarmRepository alarmRepository) : ToolFunction
{
    public override string Description => """
再生中のアラームを停止します。
アラームの停止を依頼されたときに呼び出されます。

呼び出される例: 「アラームを止めてくれない？」
""";

    public override string Parameters => """
{
    "type": "object",
    "properties": {},
    "required": [],
    "additionalProperties": false
}
""";

    private readonly IAlarmRepository _alarmRepository = alarmRepository;

    public override bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments)
    {
        arguments = new Dictionary<string, object>();
        return true;
    }

    public override async Task<string> Invoke(IDictionary<string, object> arguments, IReadOnlyConversationState state)
    {
        try
        {
            var end = DateTime.Now;
            var start = end.TimeOfDay - AlarmService.TimerInterval * 2;
            var targetSettings = await _alarmRepository.GetAlarmSettingsAsync(start, end.TimeOfDay);
            await _alarmRepository.DeleteAlarmSettingsAsync(targetSettings.Select(s => s.Id));

            return "OK";
        }
        catch (Exception)
        {
            return "ERROR";
        }
    }
}