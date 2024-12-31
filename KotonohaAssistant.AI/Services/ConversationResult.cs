using KotonohaAssistant.Core;

namespace KotonohaAssistant.AI.Services;

public class ConversationResult
{
    public required string Message { get; set; }
    public required Kotonoha Sister { get; set; }
    public required Emotion Emotion { get; set; }
    public List<ConversationFunction>? Functions { get; set; }
}

public class ConversationFunction
{
    public required string Name { get; set; }
    public required IDictionary<string, object> Arguments { get; set; }
    public required string Result { get; set; }
}