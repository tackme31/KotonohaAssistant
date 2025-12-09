using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.Core;

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
}
