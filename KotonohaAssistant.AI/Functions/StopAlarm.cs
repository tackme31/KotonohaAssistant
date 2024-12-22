using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.AI.Utils;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class StopAlarm(IAlarmRepository alarmRepository) : ToolFunction
{
    public override string Description => """
この関数は、再生中のアラームを停止するために呼び出されます。アラーム停止の依頼があった際に実行されます。

## 呼び出される例

- 「アラーム停止」
- 「アラームを止めてくれない？」

## 注意点

1. **発言制限:**  
   アラームを停止したこと以外は発言しないでください。
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

            // アラームが確実に止まるまで待機
            await Task.Delay(AlarmService.SoundInterval);

            return "アラームを停止しました";
        }
        catch (Exception)
        {
            return "アラームの停止に失敗しました";
        }
    }
}