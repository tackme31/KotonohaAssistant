using OpenAI.Chat;
using KotonohaAssistant.Core;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.AI.Extensions;

namespace KotonohaAssistant.AI.Utils;

class ConversationState()
{
    /// <summary>
    /// システム以外のメッセージ一覧
    /// </summary>
    private readonly List<ChatMessage> _chatMessages = [];

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
    public IEnumerable<ChatMessage> ChatMessages
    {
        get
        {
            var now = DateTime.Now;
            return CurrentSister switch
            {
                Kotonoha.Akane => _chatMessages.Prepend(new SystemChatMessage(SystemMessage.KotonohaAkane(now, AkaneBehaviour))),
                Kotonoha.Aoi => _chatMessages.Prepend(new SystemChatMessage(SystemMessage.KotonohaAoi(now, AoiBehaviour))),
                _ => throw new NotSupportedException()
            };
        }
    }

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

    public void AddHint(string hint)
    {
        _chatMessages.Add(new UserChatMessage($"[Hint]: {hint}"));
    }

    public void AddToolMessage(string id, string result)
    {
        _chatMessages.Add(new ToolChatMessage(id, result));
    }
}