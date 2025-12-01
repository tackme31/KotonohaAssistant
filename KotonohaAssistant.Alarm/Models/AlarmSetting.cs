using KotonohaAssistant.Core;

namespace KotonohaAssistant.Alarm.Models;

public class AlarmSetting
{
    public long Id { get; set; }
    public double TimeInSeconds { get; set; }
    public string? VoicePath { get; set; }
    public bool IsEnabled { get; set; }
}