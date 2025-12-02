namespace KotonohaAssistant.Core.Models;

public class AddAlarmRequest
{
    public long TimeInSeconds { get; set; }
    public string? VoicePath { get; set; }
    public bool IsRepeated { get; set; }
}
