using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Models;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Tests.Helpers;

/// <summary>
/// テスト用 ChatCompletion オブジェクトを生成するファクトリー
/// </summary>
public static class ChatCompletionFactory
{
    /// <summary>
    /// テスト用の ChatCompletion を作成（通常のテキスト応答）
    /// </summary>
    public static ChatCompletion CreateTextCompletion(
        Kotonoha sister,
        string text,
        ChatFinishReason finishReason = ChatFinishReason.Stop)
    {
        var response = new ChatResponse
        {
            Assistant = sister,
            Text = text
        };

        var contentJson = response.ToJson();

        return OpenAIChatModelFactory.ChatCompletion(
            id: "test-completion-id",
            model: "gpt-4",
            createdAt: DateTimeOffset.FromUnixTimeSeconds(1234567890),
            finishReason: finishReason,
            role: ChatMessageRole.Assistant,
            content: [ChatMessageContentPart.CreateTextPart(contentJson)]
        );
    }

    /// <summary>
    /// テスト用の ChatCompletion を作成（生JSONテキスト）
    /// </summary>
    public static ChatCompletion CreateRawTextCompletion(
        string text,
        ChatFinishReason finishReason = ChatFinishReason.Stop)
    {
        return OpenAIChatModelFactory.ChatCompletion(
            id: "test-completion-id",
            model: "gpt-4",
            createdAt: DateTimeOffset.FromUnixTimeSeconds(1234567890),
            finishReason: finishReason,
            role: ChatMessageRole.Assistant,
            content: [ChatMessageContentPart.CreateTextPart(text)]
        );
    }

    /// <summary>
    /// テスト用の ClientResult&lt;ChatCompletion&gt; を作成
    /// </summary>
    public static ChatCompletion CreateTextCompletionResult(
        Kotonoha sister,
        string text,
        ChatFinishReason finishReason = ChatFinishReason.Stop)
    {
        var completion = CreateTextCompletion(sister, text, finishReason);
        return completion;
    }

    /// <summary>
    /// テスト用の ChatCompletion を無効な JSON で作成
    /// </summary>
    public static ChatCompletion CreateInvalidCompletion(string invalidJson)
    {
        return OpenAIChatModelFactory.ChatCompletion(
            id: "test-completion-id",
            model: "gpt-4",
            createdAt: DateTimeOffset.FromUnixTimeSeconds(1234567890),
            finishReason: ChatFinishReason.Stop,
            role: ChatMessageRole.Assistant,
            content: [ChatMessageContentPart.CreateTextPart(invalidJson)]
        );
    }

    /// <summary>
    /// テスト用の ClientResult&lt;ChatCompletion&gt; を無効な JSON で作成
    /// </summary>
    public static ChatCompletion CreateInvalidCompletionResult(string invalidJson)
    {
        var completion = CreateInvalidCompletion(invalidJson);
        return completion;
    }

    /// <summary>
    /// テスト用の ChatCompletion を ToolCalls 付きで作成
    /// </summary>
    public static ChatCompletion CreateToolCallsCompletion(
        Kotonoha sister,
        string text,
        params (string id, string name, string arguments)[] toolCalls)
    {
        var response = new ChatResponse
        {
            Assistant = sister,
            Text = text
        };

        var contentJson = response.ToJson();

        var toolCallsList = toolCalls.Select(tc =>
            ChatToolCall.CreateFunctionToolCall(
                tc.id,
                tc.name,
                BinaryData.FromString(tc.arguments)
            )).ToArray();

        return OpenAIChatModelFactory.ChatCompletion(
            id: "test-completion-id",
            model: "gpt-4",
            createdAt: DateTimeOffset.FromUnixTimeSeconds(1234567890),
            finishReason: ChatFinishReason.ToolCalls,
            role: ChatMessageRole.Assistant,
            content: [ChatMessageContentPart.CreateTextPart(contentJson)],
            toolCalls: toolCallsList
        );
    }

    /// <summary>
    /// テスト用の ClientResult&lt;ChatCompletion&gt; を ToolCalls 付きで作成
    /// </summary>
    public static ChatCompletion CreateToolCallsCompletionResult(
        Kotonoha sister,
        string text,
        params (string id, string name, string arguments)[] toolCalls)
    {
        var completion = CreateToolCallsCompletion(sister, text, toolCalls);
        return completion;
    }

    /// <summary>
    /// テスト用の ChatCompletion を作成（Stop のみ）
    /// </summary>
    public static ChatCompletion CreateStopCompletion()
    {
        return OpenAIChatModelFactory.ChatCompletion(
            id: "test-id",
            model: "gpt-4",
            createdAt: DateTimeOffset.FromUnixTimeSeconds(1234567890),
            finishReason: ChatFinishReason.Stop,
            content: [ChatMessageContentPart.CreateTextPart("Test response")]
        );
    }

    /// <summary>
    /// テスト用の ChatCompletion を作成（ToolCalls - 簡易版）
    /// </summary>
    public static ChatCompletion CreateSimpleToolCallsCompletion(string functionName = "test_function")
    {
        return OpenAIChatModelFactory.ChatCompletion(
            id: "test-id",
            model: "gpt-4",
            createdAt: DateTimeOffset.FromUnixTimeSeconds(1234567890),
            finishReason: ChatFinishReason.ToolCalls,
            toolCalls: [
                ChatToolCall.CreateFunctionToolCall(
                    "call_123",
                    functionName,
                    BinaryData.FromObjectAsJson(new { })
                )
            ]
        );
    }

    /// <summary>
    /// テスト用の ChatCompletion を作成（複数ToolCalls）
    /// </summary>
    public static ChatCompletion CreateMultipleToolCallsCompletion(params string[] functionNames)
    {
        var toolCalls = functionNames.Select((name, index) =>
            ChatToolCall.CreateFunctionToolCall(
                $"call_{index}",
                name,
                BinaryData.FromObjectAsJson(new { })
            )).ToArray();

        return OpenAIChatModelFactory.ChatCompletion(
            id: "test-id",
            model: "gpt-4",
            createdAt: DateTimeOffset.FromUnixTimeSeconds(1234567890),
            finishReason: ChatFinishReason.ToolCalls,
            toolCalls: toolCalls
        );
    }
}
