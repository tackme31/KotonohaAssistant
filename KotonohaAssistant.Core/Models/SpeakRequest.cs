namespace KotonohaAssistant.Core.Models;

public class SpeakRequest
{
    public Kotonoha SisterType { get; set; }
    public Emotion Emotion { get; set; }
    public string? Message { get; set; }
}
