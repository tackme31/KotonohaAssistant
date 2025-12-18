using System.Collections.Immutable;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Models;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Services;

public static class ConversationStateExtensions
{
    /// <summary>
    /// 初期会話メッセージをConversationStateに読み込む
    /// </summary>
    public static void LoadInitialConversation(this ConversationState state)
    {
        foreach (var m in InitialConversation.Messages)
        {
            if (m.Request is not null)
            {
                switch (m.Request.InputType)
                {
                    case ChatInputType.Instruction:
                        state.AddInstruction(m.Request.Text ?? string.Empty);
                        continue;
                    case ChatInputType.User:
                        state.AddUserMessage(m.Request.Text ?? string.Empty);
                        continue;
                }
            }

            if (m.Response is not null)
            {
                state.AddAssistantMessage(m.Response.Assistant, m.Response.Text ?? string.Empty);
            }
        }
    }

    private const string TodayFormat = "yyyy年M月d日 (dddd)";
    private const string CurrentTimeFormat = "H時m分";

    public static ConversationState_ LoadInitialConversation(this ConversationState_ state)
    {
        var messages = new List<ChatMessage>();
        foreach (var m in InitialConversation.Messages)
        {
            if (m.Request is { InputType: not null, Text: not null })
            {
                var message = CreateUserMessage(m.Request.InputType.Value, m.Request.Text, DateTime.Now);
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

    public static ConversationState_ AddAssistantMessage(this ConversationState_ state, ChatCompletion completion)
    {
        return state with
        {
            ChatMessages = state.ChatMessages.Add(new AssistantChatMessage(completion))
        };
    }

    public static ConversationState_ AddAssistantMessage(this ConversationState_ state, Kotonoha sister, string text)
    {
        return state with
        {
            ChatMessages = state.ChatMessages.Add(CreateAssistantMessage(sister, text))
        };
    }

    public static ConversationState_ AddUserMessage(this ConversationState_ state, string text, DateTime dateTime)
    {
        return state with
        {
            ChatMessages = state.ChatMessages.Add(CreateUserMessage(ChatInputType.User, text, dateTime))
        };
    }

    public static ConversationState_ AddInstruction(this ConversationState_ state, string text, DateTime dateTime)
    {
        return state with
        {
            ChatMessages = state.ChatMessages.Add(CreateUserMessage(ChatInputType.Instruction, text, dateTime))
        };
    }

    public static ConversationState_ AddBeginLazyModeInstruction(this ConversationState_ state, DateTime dateTime)
    {
        var instruction = state.CurrentSister switch
        {
            Kotonoha.Akane => Instruction.BeginLazyModeAkane,
            Kotonoha.Aoi => Instruction.BeginLazyModeAoi,
            _ => throw new IndexOutOfRangeException()
        };

        return state.AddInstruction(instruction, dateTime);
    }

    public static ConversationState_ AddEndLazyModeInstruction(this ConversationState_ state, DateTime dateTime)
    {
        var instruction = state.CurrentSister switch
        {
            Kotonoha.Akane => Instruction.EndLazyModeAkane,
            Kotonoha.Aoi => Instruction.EndLazyModeAoi,
            _ => throw new IndexOutOfRangeException()
        };

        return state.AddInstruction(instruction, dateTime);
    }

    public static ConversationState_ AddToolMessage(this ConversationState_ state, string toolCallId, string result)
    {
        return state with
        {
            ChatMessages = state.ChatMessages.Add(new ToolChatMessage(toolCallId, result))
        };
    }

    public static ConversationState_ RecordToolCall(this ConversationState_ state, Kotonoha sister)
    {
        var patienceCount = state.LastToolCallSister == sister ? state.PatienceCount + 1 : 1;
        return state with
        {
            PatienceCount = patienceCount,
            LastToolCallSister = sister
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
