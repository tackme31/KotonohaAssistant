using System.Collections.Immutable;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Extensions;
using KotonohaAssistant.Core.Models;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Services;

public static class ConversationStateExtensions
{
    private const string TodayFormat = "yyyy年M月d日 (dddd)";
    private const string CurrentTimeFormat = "H時m分";

    public static ConversationState LoadInitialConversation(this ConversationState state, DateTime dateTime)
    {
        var messages = new List<ChatMessage>();
        foreach (var m in InitialConversation.Messages)
        {
            if (m.Request is { InputType: not null, Text: not null })
            {
                var message = CreateUserMessage(m.Request.InputType.Value, m.Request.Text, dateTime);
                messages.Add(message);
            }

            if (m.Response is { Text: not null })
            {
                var message = CreateAssistantMessage(m.Response.Assistant, m.Response.Text);
                messages.Add(message);
            }
        }

        return state with
        {
            ChatMessages = [.. messages]
        };
    }

    public static ConversationState AddAssistantMessage(this ConversationState state, ChatCompletion completion)
    {
        return state with
        {
            ChatMessages = state.ChatMessages.Add(new AssistantChatMessage(completion))
        };
    }

    public static ConversationState AddAssistantMessage(this ConversationState state, Kotonoha sister, string text)
    {
        return state with
        {
            ChatMessages = state.ChatMessages.Add(CreateAssistantMessage(sister, text))
        };
    }

    public static ConversationState AddUserMessage(this ConversationState state, string text, DateTime dateTime)
    {
        return state with
        {
            ChatMessages = state.ChatMessages.Add(CreateUserMessage(ChatInputType.User, text, dateTime))
        };
    }

    public static ConversationState AddInstruction(this ConversationState state, string text, DateTime dateTime)
    {
        return state with
        {
            ChatMessages = state.ChatMessages.Add(CreateUserMessage(ChatInputType.Instruction, text, dateTime))
        };
    }

    public static ConversationState AddBeginLazyModeInstruction(this ConversationState state, DateTime dateTime)
    {
        var instruction = state.CurrentSister switch
        {
            Kotonoha.Akane => Instruction.BeginLazyModeAkane,
            Kotonoha.Aoi => Instruction.BeginLazyModeAoi,
            _ => throw new IndexOutOfRangeException()
        };

        return state.AddInstruction(instruction, dateTime);
    }

    public static ConversationState AddEndLazyModeInstruction(this ConversationState state, DateTime dateTime)
    {
        var instruction = state.CurrentSister switch
        {
            Kotonoha.Akane => Instruction.EndLazyModeAkane,
            Kotonoha.Aoi => Instruction.EndLazyModeAoi,
            _ => throw new IndexOutOfRangeException()
        };

        return state.AddInstruction(instruction, dateTime);
    }

    public static ConversationState AddToolMessage(this ConversationState state, string toolCallId, string result)
    {
        return state with
        {
            ChatMessages = state.ChatMessages.Add(new ToolChatMessage(toolCallId, result))
        };
    }

    public static ConversationState SwitchToAnotherSister(this ConversationState state)
    {
        return state with
        {
            CurrentSister = state.CurrentSister.Switch()
        };
    }

    public static ConversationState SwitchToSister(this ConversationState state, Kotonoha sister, DateTime dateTime)
    {
        if (state.CurrentSister == sister)
            return state;

        var instruction = Instruction.SwitchSisterTo(sister);
        return state
            .AddInstruction(instruction, dateTime)
            with
        {
            CurrentSister = sister,
            PatienceCount = 0
        };
    }

    public static ConversationState RecordToolCall(this ConversationState state)
    {
        var patienceCount = state.LastToolCallSister == state.CurrentSister ? state.PatienceCount + 1 : 1;
        return state with
        {
            PatienceCount = patienceCount,
            LastToolCallSister = state.CurrentSister
        };
    }

    private static AssistantChatMessage CreateAssistantMessage(Kotonoha sister, string text)
    {
        var response = new ChatResponse
        {
            Assistant = sister,
            Text = text
        };

        return new AssistantChatMessage(response.ToJson());
    }

    private static UserChatMessage CreateUserMessage(ChatInputType type, string text, DateTime dateTime)
    {
        var request = new ChatRequest()
        {
            InputType = type,
            Text = text,
            Today = dateTime.ToString(TodayFormat),
            CurrentTime = dateTime.ToString(CurrentTimeFormat)
        };

        return new UserChatMessage(request.ToJson());
    }
}
