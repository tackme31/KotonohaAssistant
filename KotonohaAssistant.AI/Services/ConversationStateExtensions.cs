using KotonohaAssistant.AI.Prompts;

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
            if (m.Sister is not null)
            {
                state.AddAssistantMessage(m.Sister.Value, m.Text, m.Emotion);
            }
            else
            {
                state.AddUserMessage(m.Text);
            }
        }
    }
}
