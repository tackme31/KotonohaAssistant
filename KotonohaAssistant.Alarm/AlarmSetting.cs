using KotonohaAssistant.Core;

namespace KotonohaAssistant.Alarm;

public class AlarmSetting
{
    public long Id { get; set; }
    public double TimeInSeconds { get; set; }
    public Kotonoha Sister { get; set; }
    public string? Message { get; set; }
}