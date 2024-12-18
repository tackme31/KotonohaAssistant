using OpenAI.Chat;
using KotonohaAssistant.Core;
using KotonohaAssistant.AI.Prompts;

namespace KotonohaAssistant.AI.Utils;

class ChatMessageManager(
    Kotonoha defaultSister,
    string? akaneBehaviour = null,
    string? aoiBehaviour = null)
{
    private readonly List<ChatMessage> _chatMessages = [];
    private readonly string? _akaneBehaviour = akaneBehaviour;
    private readonly string? _aoiBehaviour = aoiBehaviour;

    public Kotonoha CurrentSister { get; set; } = defaultSister;

    public void AddAssistantMessage(string message)
    {
        _chatMessages.Add(new AssistantChatMessage(message));
    }

    public void AddAssistantMessage(ChatCompletion completion)
    {
        _chatMessages.Add(new AssistantChatMessage(completion));
    }

    public void AddUserMessage(string message)
    {
        _chatMessages.Add(new UserChatMessage(message));
    }

    public void AddToolMessage(string id, string result)
    {
        _chatMessages.Add(new ToolChatMessage(id, result));
    }

    public IEnumerable<ChatMessage> ChatMessages
    {
        get
        {
            var now = DateTime.Now;
            return CurrentSister switch
            {
                Kotonoha.Akane => _chatMessages.Prepend(new SystemChatMessage(SystemMessage.KotonohaAkane(now, _akaneBehaviour))),
                Kotonoha.Aoi => _chatMessages.Prepend(new SystemChatMessage(SystemMessage.KotonohaAoi(now, _aoiBehaviour))),
                _ => throw new NotSupportedException()
            };
        }
    }
}