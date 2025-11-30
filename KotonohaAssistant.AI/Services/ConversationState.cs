using KotonohaAssistant.Core;

namespace KotonohaAssistant.AI.Utils;

public interface IReadOnlyConversationState
{
    public string? AkaneBehaviour { get; }

    public string? AoiBehaviour { get; }

    public int PatienceCount { get; }

    public Kotonoha LastToolCallSister { get; }

    public Kotonoha CurrentSister { get; }
}

public class ConversationState() : IReadOnlyConversationState
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
}