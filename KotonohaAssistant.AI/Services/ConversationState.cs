using System.Collections.Immutable;
using KotonohaAssistant.Core;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Services;

public record ConversationState
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
