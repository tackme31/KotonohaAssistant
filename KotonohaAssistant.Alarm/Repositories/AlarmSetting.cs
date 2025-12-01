using KotonohaAssistant.Core;

namespace KotonohaAssistant.Alarm.Repositories;

public class AlarmSetting
{
    public long Id { get; set; }
    public double TimeInSeconds { get; set; }
    public string? VoicePath { get; set; }
}