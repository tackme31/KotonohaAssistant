using System.Collections.Immutable;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Extensions;
using KotonohaAssistant.Core.Models;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Services;

public record ConversationState_
{
    public required string SystemMessageAkane { get; init; }

    public required string SystemMessageAoi { get; init; }

    public long? ConversationId { get; set; }

    public int LastSavedMessageIndex { get; set; }

    public int PatienceCount { get; set; }

    public Kotonoha LastToolCallSister { get; set; }

    public Kotonoha CurrentSister { get; set; }

    public ImmutableList<ChatMessage> ChatMessages { get; set; } = [];

    public ImmutableList<ChatMessage> FullChatMessages
    {
        get
        {
            var systemMessage = CurrentSister switch
            {
                Kotonoha.Akane => SystemMessageAkane,
                Kotonoha.Aoi => SystemMessageAoi,
                _ => throw new IndexOutOfRangeException()
            };

            return [.. ChatMessages.Prepend(new SystemChatMessage(systemMessage))];
        }
    }
}

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
    public required string SystemMessageAkane { get; init; }
    public required string SystemMessageAoi { get; init; }

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
            return CurrentSister switch
            {
                Kotonoha.Akane => ChatMessages.Prepend(new SystemChatMessage(SystemMessageAkane)),
                Kotonoha.Aoi => ChatMessages.Prepend(new SystemChatMessage(SystemMessageAoi)),
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

    public void AddAssistantMessage(ChatCompletion completion)
    {
        _chatMessages.Add(new AssistantChatMessage(completion));
    }

    public void AddAssistantMessage(Kotonoha sister, string text)
    {
        var response = new ChatResponse
        {
            Assistant = sister,
            Text = text,
        };

        _chatMessages.Add(new AssistantChatMessage(response.ToJson()));
    }

    public void AddUserMessage(string text)
    {
        var now = DateTime.Now;
        var request = new ChatRequest
        {
            InputType = ChatInputType.User,
            Text = text,
            Today = now.ToString("yyyy年M月d日 (dddd)"),
            CurrentTime = now.ToString("H時m分")
        };

        _chatMessages.Add(new UserChatMessage(request.ToJson()));
    }

    public void AddInstruction(string text)
    {
        var now = DateTime.Now;
        var request = new ChatRequest
        {
            InputType = ChatInputType.Instruction,
            Text = text,
            Today = now.ToString("yyyy年M月d日 (dddd)"),
            CurrentTime = now.ToString("H時m分")
        };

        _chatMessages.Add(new UserChatMessage(request.ToJson()));
    }

    public void AddToolMessage(string id, string result)
    {
        _chatMessages.Add(new ToolChatMessage(id, result));
    }

    /// <summary>
    /// 関数呼び出しを記録し、忍耐値を更新します
    /// </summary>
    /// <param name="sister">関数を呼び出した姉妹</param>
    public void RecordToolCall(Kotonoha sister)
    {
        if (LastToolCallSister == sister)
        {
            PatienceCount++;
        }
        else
        {
            PatienceCount = 1;
        }
        LastToolCallSister = sister;
    }

    /// <summary>
    /// 忍耐値をリセットします
    /// </summary>
    public void ResetPatienceCount()
    {
        PatienceCount = 1;
    }

    /// <summary>
    /// 指定した姉妹に切り替えます
    /// </summary>
    /// <param name="sister">切り替え先の姉妹</param>
    public void SwitchToSister(Kotonoha sister)
    {
        CurrentSister = sister;
        AddInstruction(Instruction.SwitchSisterTo(sister));
    }

    /// <summary>
    /// もう一方の姉妹に切り替えます
    /// </summary>
    public void SwitchToOtherSister()
    {
        var nextSister = CurrentSister.Switch();
        SwitchToSister(nextSister);
    }

    /// <summary>
    /// 怠け癖モード開始の指示を追加します
    /// </summary>
    public void AddLazyModeInstruction()
    {
        var instruction = CurrentSister switch
        {
            Kotonoha.Akane => Instruction.BeginLazyModeAkane,
            Kotonoha.Aoi => Instruction.BeginLazyModeAoi,
            _ => null
        };

        if (instruction is not null)
        {
            AddInstruction(instruction);
        }
    }

    /// <summary>
    /// 怠け癖モード終了の指示を追加します
    /// </summary>
    public void AddEndLazyModeInstruction(Kotonoha sister)
    {
        var instruction = sister switch
        {
            Kotonoha.Akane => Instruction.EndLazyModeAkane,
            Kotonoha.Aoi => Instruction.EndLazyModeAoi,
            _ => null
        };

        if (instruction is not null)
        {
            AddInstruction(instruction);
        }
    }
}
