namespace KotonohaAssistant.Core.Models;

public class ExportVoiceRequest
{
    public Kotonoha SisterType { get; set; }
    public Emotion Emotion { get; set; }
    public string? Message { get; set; }
    public string? SavePath { get; set; }
}
