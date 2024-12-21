using OpenAI.Chat;
using KotonohaAssistant.Core;
using KotonohaAssistant.AI.Prompts;

namespace KotonohaAssistant.AI.Utils;

class ConversationState()
{
    /// <summary>
    /// 茜の追加の振る舞い
    /// </summary>
    public string? AkaneBehaviour { get; set; }

    /// <summary>
    /// 葵の追加の振る舞い
    /// </summary>
    public string? AoiBehaviour { get; set; }

    /// <summary>
    /// 同じ方に連続してお願いした回数。忍耐値。
    /// </summary>
    public int PatienceCount { get; set; }

    /// <summary>
    /// 最後にお願いを聞いてくれた方を格納。
    /// </summary>
    public Kotonoha LastToolCallSister { get; set; }

    /// <summary>
    /// 現在会話中の姉妹
    /// </summary>
    public Kotonoha CurrentSister { get; set; }

    /// <summary>
    /// システム付きのメッセージ一覧
    /// </summary>
    public IEnumerable<ChatMessage> ChatMessagesWithSystemMessage
    {
        get
        {
            var now = DateTime.Now;
            return CurrentSister switch
            {
                Kotonoha.Akane => ChatMessages.Prepend(new SystemChatMessage(SystemMessage.KotonohaAkane(now, AkaneBehaviour))),
                Kotonoha.Aoi => ChatMessages.Prepend(new SystemChatMessage(SystemMessage.KotonohaAoi(now, AoiBehaviour))),
                _ => throw new NotSupportedException()
            };
        }
    }

    /// <summary>
    /// システム以外のメッセージ一覧
    /// </summary>
    public List<ChatMessage> ChatMessages { get; set; } = [];

    public void LoadMessages(IEnumerable<ChatMessage> chatMessages)
    {
        ChatMessages = chatMessages.ToList();
    }

    public void AddAssistantMessage(string message)
    {
        ChatMessages.Add(new AssistantChatMessage(message));
    }

    public void AddAssistantMessage(ChatCompletion completion)
    {
        ChatMessages.Add(new AssistantChatMessage(completion));
    }

    public void AddUserMessage(string message)
    {
        ChatMessages.Add(new UserChatMessage(message));
    }

    public void AddHint(string hint)
    {
        ChatMessages.Add(new UserChatMessage($"[Hint]: {hint}"));
    }

    public void AddToolMessage(string id, string result)
    {
        ChatMessages.Add(new ToolChatMessage(id, result));
    }
}