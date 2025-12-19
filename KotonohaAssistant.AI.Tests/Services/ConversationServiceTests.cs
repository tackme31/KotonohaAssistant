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
        // Arrange
        var service = CreateService();
        var state = CreateTestState();

        // Act
        var result = service.GetAllMessages(state);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAllMessages_InitialConversationをスキップすること()
    {
        // Arrange
        var service = CreateService();
        var dateTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var state = CreateTestState();

        // InitialConversationを読み込む
        state = state.LoadInitialConversation(dateTime);

        // 実際のユーザーメッセージを1つ追加
        state = state.AddUserMessage("こんにちは", dateTime);

        // Act
        var result = service.GetAllMessages(state).ToList();

        // Assert
        // InitialConversationのメッセージ数だけスキップされるので、追加した1つだけが返される
        result.Should().HaveCount(1);
        result[0].sister.Should().BeNull();
        result[0].message.Should().Be("こんにちは");
    }

    [Fact]
    public void GetAllMessages_Contentが空のメッセージをスキップすること()
    {
        // Arrange
        var service = CreateService();
        var dateTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var state = CreateTestState();

        // InitialConversationを読み込む
        state = state.LoadInitialConversation(dateTime);

        // 正常なユーザーメッセージを追加
        state = state.AddUserMessage("正常なメッセージ", dateTime);

        // Content配列が空のメッセージを手動で追加
        var emptyContentMessage = new UserChatMessage(Array.Empty<ChatMessageContentPart>());
        state = state with
        {
            ChatMessages = state.ChatMessages.Add(emptyContentMessage)
        };

        // 別の正常なメッセージを追加
        state = state.AddUserMessage("もう一つの正常なメッセージ", dateTime);

        // Act
        var result = service.GetAllMessages(state).ToList();

        // Assert
        // 空のContentを持つメッセージはスキップされる
        result.Should().HaveCount(2);
        result[0].message.Should().Be("正常なメッセージ");
        result[1].message.Should().Be("もう一つの正常なメッセージ");
    }

    [Fact]
    public void GetAllMessages_ユーザーメッセージを正しく返すこと()
    {
        // Arrange
        var service = CreateService();
        var dateTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var state = CreateTestState();

        // InitialConversationを読み込む
        state = state.LoadInitialConversation(dateTime);
        state = state.AddUserMessage("テストメッセージ", dateTime);

        // Act
        var result = service.GetAllMessages(state).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].sister.Should().BeNull(); // ユーザーメッセージはsisterがnull
        result[0].message.Should().Be("テストメッセージ");
    }

    [Fact]
    public void GetAllMessages_アシスタントメッセージを正しく返すこと()
    {
        // Arrange
        var service = CreateService();
        var dateTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var state = CreateTestState();

        // InitialConversationを読み込む
        state = state.LoadInitialConversation(dateTime);
        state = state.AddAssistantMessage(Kotonoha.Akane, "茜のメッセージやで");

        // Act
        var result = service.GetAllMessages(state).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].sister.Should().Be(Kotonoha.Akane);
        result[0].message.Should().Be("茜のメッセージやで");
    }

    [Fact]
    public void GetAllMessages_ツールメッセージをスキップすること()
    {
        // Arrange
        var service = CreateService();
        var dateTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var state = CreateTestState();

        // InitialConversationを読み込む
        state = state.LoadInitialConversation(dateTime);

        state = state.AddUserMessage("メッセージ1", dateTime);
        state = state.AddToolMessage("call_123", "ツール実行結果");
        state = state.AddUserMessage("メッセージ2", dateTime);

        // Act
        var result = service.GetAllMessages(state).ToList();

        // Assert
        // ToolChatMessageはスキップされ、UserChatMessageのみが返される
        result.Should().HaveCount(2);
        result[0].message.Should().Be("メッセージ1");
        result[1].message.Should().Be("メッセージ2");
    }

    [Fact]
    public void GetAllMessages_複数の種類のメッセージを正しく処理すること()
    {
        // Arrange
        var service = CreateService();
        var dateTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var state = CreateTestState();

        // InitialConversationを読み込む
        state = state.LoadInitialConversation(dateTime);

        // 複数のメッセージタイプを混在させる
        state = state.AddUserMessage("ユーザー1", dateTime);
        state = state.AddAssistantMessage(Kotonoha.Akane, "茜の返答");
        state = state.AddToolMessage("call_123", "ツール結果");
        state = state.AddUserMessage("ユーザー2", dateTime);
        state = state.AddAssistantMessage(Kotonoha.Aoi, "葵の返答");

        // Act
        var result = service.GetAllMessages(state).ToList();

        // Assert
        // ToolChatMessage以外が順番通りに返される
        result.Should().HaveCount(4);

        result[0].sister.Should().BeNull();
        result[0].message.Should().Be("ユーザー1");

        result[1].sister.Should().Be(Kotonoha.Akane);
        result[1].message.Should().Be("茜の返答");

        result[2].sister.Should().BeNull();
        result[2].message.Should().Be("ユーザー2");

        result[3].sister.Should().Be(Kotonoha.Aoi);
        result[3].message.Should().Be("葵の返答");
    }

    #endregion

    #region LoadLatestConversation テスト

    [Fact]
    public async Task LoadLatestConversation_会話履歴が存在しない場合_新しい会話を作成すること()
    {
        // Arrange
        var chatMessageRepo = new MockChatMessageRepository
        {
            GetLatestConversationIdAsyncFunc = () => Task.FromResult(-1L),
            CreateNewConversationIdAsyncFunc = () => Task.FromResult(42)
        };
        var service = CreateService(chatMessageRepo: chatMessageRepo);

        // Act
        var result = await service.LoadLatestConversation();

        // Assert
        result.ConversationId.Should().Be(42);
        result.ChatMessages.Should().NotBeEmpty(); // LoadInitialConversationが呼ばれるため
        result.LastSavedMessageIndex.Should().Be(0);
    }

    [Fact]
    public async Task LoadLatestConversation_GetLatestConversationIdAsyncで例外発生時_新しい会話を作成すること()
    {
        // Arrange
        var chatMessageRepo = new MockChatMessageRepository
        {
            GetLatestConversationIdAsyncFunc = () => throw new Exception("Database error"),
            CreateNewConversationIdAsyncFunc = () => Task.FromResult(42)
        };
        var service = CreateService(chatMessageRepo: chatMessageRepo);

        // Act
        var result = await service.LoadLatestConversation();

        // Assert
        result.ConversationId.Should().Be(42);
        result.ChatMessages.Should().NotBeEmpty(); // LoadInitialConversationが呼ばれるため
        result.LastSavedMessageIndex.Should().Be(0);
    }

    [Fact]
    public async Task LoadLatestConversation_会話履歴が存在する場合_それを読み込むこと()
    {
        // Arrange
        var response = new ChatResponse
        {
            Assistant = Kotonoha.Akane,
            Text = "こんにちはやで"
        };
        var messages = new List<ChatMessage>
        {
            new UserChatMessage("テストメッセージ"),
            new AssistantChatMessage(response.ToJson())
        };

        var chatMessageRepo = new MockChatMessageRepository
        {
            GetLatestConversationIdAsyncFunc = () => Task.FromResult(123L),
            GetAllChatMessagesAsyncFunc = (conversationId) => Task.FromResult<IEnumerable<ChatMessage>>(messages)
        };
        var service = CreateService(chatMessageRepo: chatMessageRepo);

        // Act
        var result = await service.LoadLatestConversation();

        // Assert
        result.ConversationId.Should().Be(123);
        result.ChatMessages.Should().HaveCount(2);
        result.CurrentSister.Should().Be(Kotonoha.Akane);
        result.LastSavedMessageIndex.Should().Be(2);
    }

    [Fact]
    public async Task LoadLatestConversation_GetAllChatMessagesAsyncで例外発生時_デフォルトStateを返すこと()
    {
        // Arrange
        var chatMessageRepo = new MockChatMessageRepository
        {
            GetLatestConversationIdAsyncFunc = () => Task.FromResult(123L),
            GetAllChatMessagesAsyncFunc = (conversationId) => throw new Exception("Database error")
        };
        var service = CreateService(chatMessageRepo: chatMessageRepo);

        // Act
        var result = await service.LoadLatestConversation();

        // Assert
        result.ConversationId.Should().BeNull();
        result.ChatMessages.Should().BeEmpty();
        result.CurrentSister.Should().Be(Kotonoha.Akane); // デフォルト
        result.LastSavedMessageIndex.Should().Be(0);
    }

    [Fact]
    public async Task LoadLatestConversation_最後のメッセージがない場合_デフォルトの姉妹を設定すること()
    {
        // Arrange
        var chatMessageRepo = new MockChatMessageRepository
        {
            GetLatestConversationIdAsyncFunc = () => Task.FromResult(123L),
            GetAllChatMessagesAsyncFunc = (conversationId) => Task.FromResult(Enumerable.Empty<ChatMessage>())
        };
        var service = CreateService(chatMessageRepo: chatMessageRepo);

        // Act
        var result = await service.LoadLatestConversation();

        // Assert
        result.ConversationId.Should().Be(123);
        result.ChatMessages.Should().BeEmpty();
        result.CurrentSister.Should().Be(Kotonoha.Akane); // デフォルト
        result.LastSavedMessageIndex.Should().Be(0);
    }

    [Fact]
    public async Task LoadLatestConversation_最後のメッセージから現在の姉妹を復元すること()
    {
        // Arrange
        var response = new ChatResponse
        {
            Assistant = Kotonoha.Aoi,
            Text = "はい、わかりました"
        };
        var messages = new List<ChatMessage>
        {
            new UserChatMessage("テストメッセージ"),
            new AssistantChatMessage(response.ToJson())
        };

        var chatMessageRepo = new MockChatMessageRepository
        {
            GetLatestConversationIdAsyncFunc = () => Task.FromResult(123L),
            GetAllChatMessagesAsyncFunc = (conversationId) => Task.FromResult<IEnumerable<ChatMessage>>(messages)
        };
        var service = CreateService(chatMessageRepo: chatMessageRepo);

        // Act
        var result = await service.LoadLatestConversation();

        // Assert
        result.ConversationId.Should().Be(123);
        result.CurrentSister.Should().Be(Kotonoha.Aoi);
        result.ChatMessages.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadLatestConversation_最後のメッセージがパースできない場合_デフォルトの姉妹を設定すること()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new UserChatMessage("テストメッセージ"),
            new AssistantChatMessage("これは無効なJSON形式です")
        };

        var chatMessageRepo = new MockChatMessageRepository
        {
            GetLatestConversationIdAsyncFunc = () => Task.FromResult(123L),
            GetAllChatMessagesAsyncFunc = (conversationId) => Task.FromResult<IEnumerable<ChatMessage>>(messages)
        };
        var service = CreateService(chatMessageRepo: chatMessageRepo);

        // Act
        var result = await service.LoadLatestConversation();

        // Assert
        result.ConversationId.Should().Be(123);
        result.CurrentSister.Should().Be(Kotonoha.Akane); // デフォルト
        result.ChatMessages.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadLatestConversation_LastSavedMessageIndexが正しく設定されること()
    {
        // Arrange
        var response = new ChatResponse
        {
            Assistant = Kotonoha.Akane,
            Text = "テスト応答"
        };
        var messages = new List<ChatMessage>
        {
            new UserChatMessage("メッセージ1"),
            new AssistantChatMessage(response.ToJson()),
            new UserChatMessage("メッセージ2"),
            new AssistantChatMessage(response.ToJson()),
            new UserChatMessage("メッセージ3")
        };

        var chatMessageRepo = new MockChatMessageRepository
        {
            GetLatestConversationIdAsyncFunc = () => Task.FromResult(123L),
            GetAllChatMessagesAsyncFunc = (conversationId) => Task.FromResult<IEnumerable<ChatMessage>>(messages)
        };
        var service = CreateService(chatMessageRepo: chatMessageRepo);

        // Act
        var result = await service.LoadLatestConversation();

        // Assert
        result.ConversationId.Should().Be(123);
        result.LastSavedMessageIndex.Should().Be(5); // メッセージ数と一致
        result.ChatMessages.Should().HaveCount(5);
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
        // Arrange
        var service = CreateService();
        var dateTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var state = CreateTestState(currentSister: Kotonoha.Akane);

        // Act
        var result = service.TrySwitchSister("茜ちゃん、おはよう", dateTime, state);

        // Assert
        result.Should().Be(state);
        result.CurrentSister.Should().Be(Kotonoha.Akane);
        result.ChatMessages.Should().BeEmpty();
    }

    [Fact]
    public void TrySwitchSister_茜ちゃん指定で茜に切り替わること()
    {
        // Arrange
        var service = CreateService();
        var dateTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var state = CreateTestState(currentSister: Kotonoha.Aoi);

        // Act
        var result = service.TrySwitchSister("茜ちゃん、お願い", dateTime, state);

        // Assert
        result.CurrentSister.Should().Be(Kotonoha.Akane);
        result.PatienceCount.Should().Be(0);
        result.ChatMessages.Should().HaveCount(1);
        result.ChatMessages[0].Should().BeOfType<UserChatMessage>();
    }

    [Fact]
    public void TrySwitchSister_葵ちゃん指定で葵に切り替わること()
    {
        // Arrange
        var service = CreateService();
        var dateTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var state = CreateTestState(currentSister: Kotonoha.Akane);

        // Act
        var result = service.TrySwitchSister("葵ちゃん、教えて", dateTime, state);

        // Assert
        result.CurrentSister.Should().Be(Kotonoha.Aoi);
        result.PatienceCount.Should().Be(0);
        result.ChatMessages.Should().HaveCount(1);
        result.ChatMessages[0].Should().BeOfType<UserChatMessage>();
    }

    [Fact]
    public void TrySwitchSister_あかねちゃん指定で茜に切り替わること()
    {
        // Arrange
        var service = CreateService();
        var dateTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var state = CreateTestState(currentSister: Kotonoha.Aoi);

        // Act
        var result = service.TrySwitchSister("あかねちゃん、お願い", dateTime, state);

        // Assert
        result.CurrentSister.Should().Be(Kotonoha.Akane);
        result.PatienceCount.Should().Be(0);
        result.ChatMessages.Should().HaveCount(1);
        result.ChatMessages[0].Should().BeOfType<UserChatMessage>();
    }

    [Fact]
    public void TrySwitchSister_あおいちゃん指定で葵に切り替わること()
    {
        // Arrange
        var service = CreateService();
        var dateTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var state = CreateTestState(currentSister: Kotonoha.Akane);

        // Act
        var result = service.TrySwitchSister("あおいちゃん、教えて", dateTime, state);

        // Assert
        result.CurrentSister.Should().Be(Kotonoha.Aoi);
        result.PatienceCount.Should().Be(0);
        result.ChatMessages.Should().HaveCount(1);
        result.ChatMessages[0].Should().BeOfType<UserChatMessage>();
    }

    [Fact]
    public void TrySwitchSister_どちらも含まれていない場合_何も変更しないこと()
    {
        // Arrange
        var service = CreateService();
        var dateTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var state = CreateTestState(currentSister: Kotonoha.Akane);

        // Act
        var result = service.TrySwitchSister("こんにちは", dateTime, state);

        // Assert
        result.Should().Be(state);
        result.CurrentSister.Should().Be(Kotonoha.Akane);
        result.ChatMessages.Should().BeEmpty();
    }

    [Fact]
    public void TrySwitchSister_両方含まれている場合_最初にヒットした方に切り替わること()
    {
        // Arrange
        var service = CreateService();
        var dateTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var state = CreateTestState(currentSister: Kotonoha.Aoi);

        // Act - 「茜ちゃん」が先に出現
        var result = service.TrySwitchSister("茜ちゃんと葵ちゃん", dateTime, state);

        // Assert
        result.CurrentSister.Should().Be(Kotonoha.Akane);
        result.ChatMessages.Should().HaveCount(1);
    }

    [Fact]
    public void TrySwitchSister_GuessTargetSisterがnullを返す場合_何も変更しないこと()
    {
        // Arrange
        var service = CreateService();
        var dateTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var state = CreateTestState(currentSister: Kotonoha.Akane);

        // Act - 姉妹名を含まない入力
        var result = service.TrySwitchSister("今日の天気は？", dateTime, state);

        // Assert
        result.Should().Be(state);
        result.CurrentSister.Should().Be(Kotonoha.Akane);
        result.ChatMessages.Should().BeEmpty();
    }

    #endregion
}
