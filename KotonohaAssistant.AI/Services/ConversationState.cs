using OpenAI.Chat;
using KotonohaAssistant.Core;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Models;

namespace KotonohaAssistant.AI.Utils;

public interface IReadOnlyConversationState
{

    public int PatienceCount { get; }

    public Kotonoha LastToolCallSister { get; }

    public Kotonoha CurrentSister { get; }

    public IEnumerable<ChatMessage> ChatMessagesWithSystemMessage { get; }

    public IEnumerable<ChatMessage> ChatMessages { get; }
}

public class ConversationState() : IReadOnlyConversationState
{
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
                Kotonoha.Akane => ChatMessages.Prepend(new SystemChatMessage(SystemMessage.KotonohaAkane(now))),
                Kotonoha.Aoi => ChatMessages.Prepend(new SystemChatMessage(SystemMessage.KotonohaAoi(now))),
                _ => throw new NotSupportedException()
            };
        }
    }

    /// <summary>
    /// システムメッセージ以外のメッセージ一覧
    /// </summary>
    private List<ChatMessage> _chatMessages = [];

    /// <summary>
    /// システム以外のメッセージ一覧
    /// </summary>
    public IEnumerable<ChatMessage> ChatMessages => _chatMessages;

    public void ClearChatMessages()
    {
        _chatMessages.Clear();
    }

    public void LoadMessages(IEnumerable<ChatMessage> chatMessages)
    {
        _chatMessages = chatMessages.ToList();
    }

    public void AddMessage(ChatCompletion completion)
    {
        _chatMessages.Add(new AssistantChatMessage(completion));
    }

    public void AddMessage(ChatResponse response)
    {
        _chatMessages.Add(new AssistantChatMessage(response.ToJson()));
    }

    public void AddMessage(ChatRequest request)
    {
        _chatMessages.Add(new UserChatMessage(request.ToJson()));
    }

    public void AddToolMessage(string id, string result)
    {
        _chatMessages.Add(new ToolChatMessage(id, result));
    }
}