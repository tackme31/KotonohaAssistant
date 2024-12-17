using OpenAI.Chat;
using System.Collections.Generic;
using System;
using System.Linq;
using KotonohaAssistant.Core;

class ChatMessageManager(Kotonoha defaultSister)
{
    private readonly List<ChatMessage> _chatMessages = [];
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

    public IEnumerable<ChatMessage> ChatMessages => CurrentSister switch
    {
        Kotonoha.Akane => _chatMessages.Prepend(new SystemChatMessage(SystemMessage.KotonohaAkane)),
        Kotonoha.Aoi => _chatMessages.Prepend(new SystemChatMessage(SystemMessage.KotonohaAoi)),
        _ => throw new NotSupportedException()
    };
}