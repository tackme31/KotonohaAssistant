using System.ClientModel;
using System.Collections.Immutable;
using System.Text.Json;
using FluentAssertions;
using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Models;
using KotonohaAssistant.Core.Utils;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Tests.Services;

public class ConversationServiceTests
{
    #region Test Helpers

    /// <summary>
    /// テスト用のモック ILogger
    /// </summary>
    private class MockLogger : ILogger
    {
        public void LogInformation(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message) { }
        public void LogError(Exception exception) { }
    }

    /// <summary>
    /// テスト用のモック IChatMessageRepository
    /// </summary>
    private class MockChatMessageRepository : IChatMessageRepository
    {
        public Func<Task<int>>? CreateNewConversationIdAsyncFunc { get; set; }
        public Func<Task<long>>? GetLatestConversationIdAsyncFunc { get; set; }
        public Func<long, Task<IEnumerable<ChatMessage>>>? GetAllChatMessagesAsyncFunc { get; set; }
        public Func<long, Task<IEnumerable<Message>>>? GetAllMessageAsyncFunc { get; set; }
        public Func<IEnumerable<ChatMessage>, long, Task>? InsertChatMessagesAsyncFunc { get; set; }

        public Task<int> CreateNewConversationIdAsync()
        {
            return CreateNewConversationIdAsyncFunc?.Invoke() ?? Task.FromResult(1);
        }

        public Task<long> GetLatestConversationIdAsync()
        {
            return GetLatestConversationIdAsyncFunc?.Invoke() ?? Task.FromResult(-1L);
        }

        public Task<IEnumerable<ChatMessage>> GetAllChatMessagesAsync(long conversationId)
        {
            return GetAllChatMessagesAsyncFunc?.Invoke(conversationId) ?? Task.FromResult(Enumerable.Empty<ChatMessage>());
        }

        public Task<IEnumerable<Message>> GetAllMessageAsync(long conversationId)
        {
            return GetAllMessageAsyncFunc?.Invoke(conversationId) ?? Task.FromResult(Enumerable.Empty<Message>());
        }

        public Task InsertChatMessagesAsync(IEnumerable<ChatMessage> messages, long conversationId)
        {
            return InsertChatMessagesAsyncFunc?.Invoke(messages, conversationId) ?? Task.CompletedTask;
        }
    }

    /// <summary>
    /// テスト用のモック IChatCompletionRepository
    /// </summary>
    private class MockChatCompletionRepository : IChatCompletionRepository
    {
        public Func<IEnumerable<ChatMessage>, ChatCompletionOptions?, Task<ClientResult<ChatCompletion>>>? CompleteChatAsyncFunc { get; set; }

        public Task<ClientResult<ChatCompletion>> CompleteChatAsync(IEnumerable<ChatMessage> messages, ChatCompletionOptions? options = null)
        {
            if (CompleteChatAsyncFunc != null)
            {
                return CompleteChatAsyncFunc(messages, options);
            }

            throw new NotImplementedException("CompleteChatAsyncFunc is not set");
        }
    }

    /// <summary>
    /// テスト用のモック IPromptRepository
    /// </summary>
    private class MockPromptRepository : IPromptRepository
    {
        public string GetSystemMessage(Kotonoha kotonoha)
        {
            return kotonoha == Kotonoha.Akane
                ? "System message for Akane"
                : "System message for Aoi";
        }

        public string MakeTimeBasedPromise => "Mock MakeTimeBasedPromise";
        public string CreateCalendarEventDescription => "Mock CreateCalendarEventDescription";
        public string ForgetMemoryDescription => "Mock ForgetMemoryDescription";
        public string GetCalendarEventDescription => "Mock GetCalendarEventDescription";
        public string GetWeatherDescription => "Mock GetWeatherDescription";
        public string StartTimerDescription => "Mock StartTimerDescription";
        public string StopAlarmDescription => "Mock StopAlarmDescription";
        public string StopTimerDescription => "Mock StopTimerDescription";
        public string InactiveNotification => "Mock InactiveNotification";
    }

    /// <summary>
    /// テスト用のモック ILazyModeHandler
    /// </summary>
    private class MockLazyModeHandler : ILazyModeHandler
    {
        public Func<ChatCompletion, ConversationState, DateTime, Func<ConversationState, Task<ChatCompletion?>>, Task<(LazyModeResult, ConversationState)>>? HandleLazyModeAsyncFunc { get; set; }

        public Task<(LazyModeResult, ConversationState)> HandleLazyModeAsync(
            ChatCompletion completion,
            ConversationState state,
            DateTime dateTime,
            Func<ConversationState, Task<ChatCompletion?>> regenerateCompletionAsync)
        {
            if (HandleLazyModeAsyncFunc != null)
            {
                return HandleLazyModeAsyncFunc(completion, state, dateTime, regenerateCompletionAsync);
            }

            var result = new LazyModeResult
            {
                WasLazy = false,
                LazyResponse = null,
                FinalCompletion = completion
            };
            return Task.FromResult((result, state));
        }
    }

    /// <summary>
    /// テスト用のモック IDateTimeProvider
    /// </summary>
    private class MockDateTimeProvider : IDateTimeProvider
    {
        private readonly DateTime _now;

        public MockDateTimeProvider(DateTime now)
        {
            _now = now;
        }

        public DateTime Now => _now;
    }

    /// <summary>
    /// テスト用の ConversationState を作成
    /// </summary>
    private ConversationState CreateTestState(
        Kotonoha currentSister = Kotonoha.Akane,
        int patienceCount = 0,
        long? conversationId = null)
    {
        return new ConversationState
        {
            SystemMessageAkane = "System message for Akane",
            SystemMessageAoi = "System message for Aoi",
            CurrentSister = currentSister,
            PatienceCount = patienceCount,
            ConversationId = conversationId,
            ChatMessages = ImmutableArray<ChatMessage>.Empty,
            LastSavedMessageIndex = 0,
            LastToolCallSister = Kotonoha.Akane
        };
    }

    /// <summary>
    /// テスト用の ConversationService を作成
    /// </summary>
    private ConversationService CreateService(
        MockChatMessageRepository? chatMessageRepo = null,
        MockChatCompletionRepository? chatCompletionRepo = null,
        MockPromptRepository? promptRepo = null,
        MockLazyModeHandler? lazyModeHandler = null,
        MockDateTimeProvider? dateTimeProvider = null)
    {
        chatMessageRepo ??= new MockChatMessageRepository();
        chatCompletionRepo ??= new MockChatCompletionRepository();
        promptRepo ??= new MockPromptRepository();
        lazyModeHandler ??= new MockLazyModeHandler();
        dateTimeProvider ??= new MockDateTimeProvider(new DateTime(2025, 1, 1, 12, 0, 0));

        return new ConversationService(
            promptRepo,
            chatMessageRepo,
            chatCompletionRepo,
            new List<ToolFunction>(),
            lazyModeHandler,
            dateTimeProvider,
            new MockLogger()
        );
    }

    #endregion

    #region GetAllMessages テスト

    [Fact]
    public void GetAllMessages_空の状態で空のリストを返すこと()
    {
        // 試験内容: ChatMessagesが空の状態でGetAllMessagesを呼び出す
        // 期待される結果: 空のコレクションが返される
        throw new NotImplementedException();
    }

    [Fact]
    public void GetAllMessages_InitialConversationをスキップすること()
    {
        // 試験内容: InitialConversationを含む状態でGetAllMessagesを呼び出す
        // 期待される結果: InitialConversation.Countの分だけメッセージがスキップされる
        throw new NotImplementedException();
    }

    [Fact]
    public void GetAllMessages_Contentが空のメッセージをスキップすること()
    {
        // 試験内容: Content配列が空のメッセージを含む状態でGetAllMessagesを呼び出す
        // 期待される結果: そのメッセージがスキップされる
        throw new NotImplementedException();
    }

    [Fact]
    public void GetAllMessages_ユーザーメッセージを正しく返すこと()
    {
        // 試験内容: UserChatMessageを含む状態でGetAllMessagesを呼び出す
        // 期待される結果: (sister: null, message: テキスト) のタプルが返される
        throw new NotImplementedException();
    }

    [Fact]
    public void GetAllMessages_アシスタントメッセージを正しく返すこと()
    {
        // 試験内容: AssistantChatMessageを含む状態でGetAllMessagesを呼び出す
        // 期待される結果: (sister: Kotonoha, message: テキスト) のタプルが返される
        throw new NotImplementedException();
    }

    [Fact]
    public void GetAllMessages_ツールメッセージをスキップすること()
    {
        // 試験内容: ToolChatMessageを含む状態でGetAllMessagesを呼び出す
        // 期待される結果: ToolChatMessageがスキップされ、返されない
        throw new NotImplementedException();
    }

    [Fact]
    public void GetAllMessages_複数の種類のメッセージを正しく処理すること()
    {
        // 試験内容: ユーザー、アシスタント、ツールメッセージが混在する状態でGetAllMessagesを呼び出す
        // 期待される結果: ツールメッセージ以外が順番通りに返される
        throw new NotImplementedException();
    }

    #endregion

    #region LoadLatestConversation テスト

    [Fact]
    public async Task LoadLatestConversation_会話履歴が存在しない場合_新しい会話を作成すること()
    {
        // 試験内容: GetLatestConversationIdAsyncが-1を返す場合にLoadLatestConversationを呼び出す
        // 期待される結果: CreateNewConversationAsyncが呼ばれ、新しいConversationIdが設定される
        throw new NotImplementedException();
    }

    [Fact]
    public async Task LoadLatestConversation_GetLatestConversationIdAsyncで例外発生時_新しい会話を作成すること()
    {
        // 試験内容: GetLatestConversationIdAsyncが例外をスローした場合にLoadLatestConversationを呼び出す
        // 期待される結果: 例外がキャッチされ、新しい会話が作成される
        throw new NotImplementedException();
    }

    [Fact]
    public async Task LoadLatestConversation_会話履歴が存在する場合_それを読み込むこと()
    {
        // 試験内容: 有効なconversationIdが返され、メッセージが取得できる場合にLoadLatestConversationを呼び出す
        // 期待される結果: ConversationStateにconversationIdとメッセージが設定される
        throw new NotImplementedException();
    }

    [Fact]
    public async Task LoadLatestConversation_GetAllChatMessagesAsyncで例外発生時_デフォルトStateを返すこと()
    {
        // 試験内容: GetAllChatMessagesAsyncが例外をスローした場合にLoadLatestConversationを呼び出す
        // 期待される結果: 例外がキャッチされ、デフォルトStateが返される
        throw new NotImplementedException();
    }

    [Fact]
    public async Task LoadLatestConversation_最後のメッセージがない場合_デフォルトの姉妹を設定すること()
    {
        // 試験内容: メッセージが空の場合にLoadLatestConversationを呼び出す
        // 期待される結果: CurrentSisterがデフォルト（Akane）に設定される
        throw new NotImplementedException();
    }

    [Fact]
    public async Task LoadLatestConversation_最後のメッセージから現在の姉妹を復元すること()
    {
        // 試験内容: 最後のメッセージがAssistantChatMessageで、Aoiの応答を含む場合にLoadLatestConversationを呼び出す
        // 期待される結果: CurrentSisterがAoiに設定される
        throw new NotImplementedException();
    }

    [Fact]
    public async Task LoadLatestConversation_最後のメッセージがパースできない場合_デフォルトの姉妹を設定すること()
    {
        // 試験内容: 最後のメッセージのContentがChatResponseとしてパースできない場合にLoadLatestConversationを呼び出す
        // 期待される結果: CurrentSisterがデフォルト（Akane）に設定される
        throw new NotImplementedException();
    }

    [Fact]
    public async Task LoadLatestConversation_LastSavedMessageIndexが正しく設定されること()
    {
        // 試験内容: メッセージが複数ある場合にLoadLatestConversationを呼び出す
        // 期待される結果: LastSavedMessageIndexがメッセージ数と一致する
        throw new NotImplementedException();
    }

    #endregion

    #region TalkAsync テスト

    [Fact]
    public async Task TalkAsync_空の入力で何も返さないこと()
    {
        // 試験内容: 空文字列またはnullを入力としてTalkAsyncを呼び出す
        // 期待される結果: (state, null) が1回だけ返される
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkAsync_ConversationIdがnullの場合_新しい会話を作成すること()
    {
        // 試験内容: ConversationIdがnullの状態でTalkAsyncを呼び出す
        // 期待される結果: CreateNewConversationAsyncが呼ばれ、ConversationIdが設定される
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkAsync_姉妹切り替えが実行されること()
    {
        // 試験内容: 「茜ちゃん」を含む入力でTalkAsyncを呼び出す
        // 期待される結果: TrySwitchSisterが呼ばれ、CurrentSisterが更新される
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkAsync_ユーザーメッセージが追加されること()
    {
        // 試験内容: 通常の入力でTalkAsyncを呼び出す
        // 期待される結果: ChatMessagesにUserChatMessageが追加される
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkAsync_CompleteChatAsyncがnullを返す場合_nullを返すこと()
    {
        // 試験内容: CompleteChatAsyncがnullを返す場合にTalkAsyncを呼び出す
        // 期待される結果: (state, null) が返される
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkAsync_PatienceCountが正しく更新されること()
    {
        // 試験内容: FinishReason=ToolCallsのCompletionが返される場合にTalkAsyncを呼び出す
        // 期待される結果: PatienceCountがインクリメントされる
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkAsync_LazyModeHandlerが呼ばれること()
    {
        // 試験内容: ToolCallsを含むCompletionでTalkAsyncを呼び出す
        // 期待される結果: LazyModeHandler.HandleLazyModeAsyncが呼ばれる
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkAsync_怠け癖応答が返されること()
    {
        // 試験内容: LazyModeHandlerがLazyResponseを返す場合にTalkAsyncを呼び出す
        // 期待される結果: LazyResponseが返され、PatienceCountがリセットされる
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkAsync_FinalCompletionがnullの場合_nullを返すこと()
    {
        // 試験内容: LazyModeHandlerがFinalCompletion=nullを返す場合にTalkAsyncを呼び出す
        // 期待される結果: (state, null) が返される
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkAsync_AssistantMessageが追加されること()
    {
        // 試験内容: 正常なCompletionが返される場合にTalkAsyncを呼び出す
        // 期待される結果: ChatMessagesにAssistantChatMessageが追加される
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkAsync_関数が実行されること()
    {
        // 試験内容: ToolCallsを含むCompletionでTalkAsyncを呼び出す
        // 期待される結果: InvokeFunctionsが呼ばれ、関数が実行される
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkAsync_ForgetMemory実行時_新しい会話が作成されること()
    {
        // 試験内容: ForgetMemoryが成功した場合にTalkAsyncを呼び出す
        // 期待される結果: CreateNewConversationAsyncが呼ばれ、ConversationIdが新しくなる
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkAsync_状態が保存されること()
    {
        // 試験内容: 正常にTalkAsyncが完了する
        // 期待される結果: SaveStateが呼ばれ、未保存メッセージがデータベースに保存される
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkAsync_最終結果が正しく返されること()
    {
        // 試験内容: 正常なCompletionでTalkAsyncを呼び出す
        // 期待される結果: ConversationResultが返され、Message、Sister、Functionsが設定される
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkAsync_ChatResponseのパースに失敗した場合_nullを返すこと()
    {
        // 試験内容: CompletionのContentがChatResponseとしてパースできない場合にTalkAsyncを呼び出す
        // 期待される結果: (state, null) が返される
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkAsync_複数回の関数呼び出しループが処理されること()
    {
        // 試験内容: InvokeFunctionsで複数回ToolCallsが返される場合にTalkAsyncを呼び出す
        // 期待される結果: すべての関数呼び出しが処理され、最終的にStopになるまで繰り返される
        throw new NotImplementedException();
    }

    #endregion

    #region TrySwitchSister テスト

    [Fact]
    public void TrySwitchSister_同じ姉妹の場合_何も変更しないこと()
    {
        // 試験内容: CurrentSisterがAkaneで「茜ちゃん」を含む入力でTrySwitchSisterを呼び出す
        // 期待される結果: Stateがそのまま返され、変更されない
        throw new NotImplementedException();
    }

    [Fact]
    public void TrySwitchSister_茜ちゃん指定で茜に切り替わること()
    {
        // 試験内容: CurrentSisterがAoiで「茜ちゃん」を含む入力でTrySwitchSisterを呼び出す
        // 期待される結果: CurrentSisterがAkaneに切り替わり、インストラクションが追加される
        throw new NotImplementedException();
    }

    [Fact]
    public void TrySwitchSister_葵ちゃん指定で葵に切り替わること()
    {
        // 試験内容: CurrentSisterがAkaneで「葵ちゃん」を含む入力でTrySwitchSisterを呼び出す
        // 期待される結果: CurrentSisterがAoiに切り替わり、インストラクションが追加される
        throw new NotImplementedException();
    }

    [Fact]
    public void TrySwitchSister_あかねちゃん指定で茜に切り替わること()
    {
        // 試験内容: CurrentSisterがAoiで「あかねちゃん」を含む入力でTrySwitchSisterを呼び出す
        // 期待される結果: CurrentSisterがAkaneに切り替わる
        throw new NotImplementedException();
    }

    [Fact]
    public void TrySwitchSister_あおいちゃん指定で葵に切り替わること()
    {
        // 試験内容: CurrentSisterがAkaneで「あおいちゃん」を含む入力でTrySwitchSisterを呼び出す
        // 期待される結果: CurrentSisterがAoiに切り替わる
        throw new NotImplementedException();
    }

    [Fact]
    public void TrySwitchSister_どちらも含まれていない場合_何も変更しないこと()
    {
        // 試験内容: 姉妹名を含まない入力でTrySwitchSisterを呼び出す
        // 期待される結果: Stateがそのまま返される
        throw new NotImplementedException();
    }

    [Fact]
    public void TrySwitchSister_両方含まれている場合_最初にヒットした方に切り替わること()
    {
        // 試験内容: 「茜ちゃんと葵ちゃん」のように両方を含む入力でTrySwitchSisterを呼び出す
        // 期待される結果: 最初に出現した方（茜）に切り替わる
        throw new NotImplementedException();
    }

    [Fact]
    public void TrySwitchSister_GuessTargetSisterがnullを返す場合_何も変更しないこと()
    {
        // 試験内容: GuessTargetSisterがnullを返す入力でTrySwitchSisterを呼び出す
        // 期待される結果: Stateがそのまま返される
        throw new NotImplementedException();
    }

    #endregion
}
