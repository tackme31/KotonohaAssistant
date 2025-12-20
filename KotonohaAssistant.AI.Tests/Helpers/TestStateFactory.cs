using System.Collections.Immutable;
using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Utils;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Tests.Helpers;

/// <summary>
/// テスト用 ConversationState オブジェクトを生成するファクトリー
/// </summary>
public static class TestStateFactory
{
    /// <summary>
    /// テスト用の ConversationState を作成
    /// </summary>
    public static ConversationState CreateTestState(
        Kotonoha currentSister = Kotonoha.Akane,
        int patienceCount = 0,
        long? conversationId = null,
        int lastSavedMessageIndex = 0,
        Kotonoha? lastToolCallSister = null)
    {
        return new ConversationState
        {
            SystemMessageAkane = "System message for Akane",
            SystemMessageAoi = "System message for Aoi",
            CurrentSister = currentSister,
            PatienceCount = patienceCount,
            ConversationId = conversationId,
            ChatMessages = ImmutableArray<ChatMessage>.Empty,
            LastSavedMessageIndex = lastSavedMessageIndex,
            LastToolCallSister = lastToolCallSister ?? Kotonoha.Akane
        };
    }

    /// <summary>
    /// テスト用の関数辞書を作成
    /// </summary>
    public static Dictionary<string, ToolFunction> CreateFunctionDictionary(
        bool canBeLazy = true,
        ILogger? logger = null)
    {
        logger ??= new MockLogger();
        return new Dictionary<string, ToolFunction>
        {
            ["test_function"] = new MockToolFunction(canBeLazy, logger)
        };
    }
}
