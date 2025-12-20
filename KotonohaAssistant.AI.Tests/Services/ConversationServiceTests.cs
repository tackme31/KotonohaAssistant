using System.ClientModel;
using System.Text.Json.Nodes;
using FluentAssertions;
using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.AI.Tests.Helpers;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Models;
using KotonohaAssistant.Core.Utils;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Tests.Services;

public class ConversationServiceTests
{
    #region Test Helpers

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
            [],
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
        var state = TestStateFactory.CreateTestState();

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
        var state = TestStateFactory.CreateTestState();

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
        var state = TestStateFactory.CreateTestState();

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
        var state = TestStateFactory.CreateTestState();

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
        var state = TestStateFactory.CreateTestState();

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
        var state = TestStateFactory.CreateTestState();

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
        var state = TestStateFactory.CreateTestState();

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
        // Arrange
        var service = CreateService();
        var state = TestStateFactory.CreateTestState();

        // Act
        var results = new List<(ConversationState state, ConversationResult? result)>();
        await foreach (var item in service.TalkAsync("", state))
        {
            results.Add(item);
        }

        // Assert
        results.Should().HaveCount(1);
        results[0].result.Should().BeNull();
        results[0].state.Should().BeEquivalentTo(state); // 状態は変更されない
    }

    [Fact]
    public async Task TalkAsync_ConversationIdがnullの場合_新しい会話を作成すること()
    {
        // Arrange
        var chatMessageRepo = new MockChatMessageRepository
        {
            CreateNewConversationIdAsyncFunc = () => Task.FromResult(42)
        };
        var chatCompletionRepo = new MockChatCompletionRepository
        {
            CompleteChatAsyncFunc = (messages, options) =>
                Task.FromResult(ChatCompletionFactory.CreateTextCompletionResult(Kotonoha.Akane, "こんにちはやで"))
        };
        var service = CreateService(chatMessageRepo: chatMessageRepo, chatCompletionRepo: chatCompletionRepo);
        var state = TestStateFactory.CreateTestState(conversationId: null);

        // Act
        var results = new List<(ConversationState state, ConversationResult? result)>();
        await foreach (var item in service.TalkAsync("こんにちは", state))
        {
            results.Add(item);
        }

        // Assert
        results.Should().NotBeEmpty();
        results.Last().state.ConversationId.Should().Be(42);
    }

    [Fact]
    public async Task TalkAsync_姉妹切り替えが実行されること()
    {
        // Arrange
        var chatCompletionRepo = new MockChatCompletionRepository
        {
            CompleteChatAsyncFunc = (messages, options) =>
                Task.FromResult(ChatCompletionFactory.CreateTextCompletionResult(Kotonoha.Akane, "はいはい、茜やで"))
        };
        var service = CreateService(chatCompletionRepo: chatCompletionRepo);
        var state = TestStateFactory.CreateTestState(currentSister: Kotonoha.Aoi, conversationId: 1);

        // Act
        var results = new List<(ConversationState state, ConversationResult? result)>();
        await foreach (var item in service.TalkAsync("茜ちゃん、お願い", state))
        {
            results.Add(item);
        }

        // Assert
        results.Should().NotBeEmpty();
        var finalState = results.Last().state;

        // 葵 → 茜 に切り替わっていることを確認
        finalState.CurrentSister.Should().Be(Kotonoha.Akane);

        // ChatMessages に姉妹切り替えのメッセージが追加されていることを確認
        var userMessages = finalState.ChatMessages
            .OfType<UserChatMessage>()
            .ToList();
        userMessages.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task TalkAsync_ユーザーメッセージが追加されること()
    {
        // Arrange
        var chatCompletionRepo = new MockChatCompletionRepository
        {
            CompleteChatAsyncFunc = (messages, options) =>
                Task.FromResult(ChatCompletionFactory.CreateTextCompletionResult(Kotonoha.Akane, "わかったで"))
        };
        var service = CreateService(chatCompletionRepo: chatCompletionRepo);
        var state = TestStateFactory.CreateTestState(conversationId: 1);

        // Act
        var results = new List<(ConversationState state, ConversationResult? result)>();
        await foreach (var item in service.TalkAsync("テスト入力", state))
        {
            results.Add(item);
        }

        // Assert
        results.Should().NotBeEmpty();
        var finalState = results.Last().state;

        // ChatMessages に UserChatMessage が追加されていることを確認
        var userMessages = finalState.ChatMessages
            .OfType<UserChatMessage>()
            .ToList();
        userMessages.Should().NotBeEmpty();

        // 最後に追加されたユーザーメッセージの内容を確認
        var lastUserMessage = userMessages.Last().Content.AsJson();
        lastUserMessage.Should().NotBeNull();
        lastUserMessage.Should().HavePropertyWithStringValue("Text", "テスト入力");
    }

    [Fact]
    public async Task TalkAsync_CompleteChatAsyncがnullを返す場合_nullを返すこと()
    {
        // Arrange
        var chatCompletionRepo = new MockChatCompletionRepository
        {
            CompleteChatAsyncFunc = (messages, options) =>
                throw new Exception("API Error")
        };
        var service = CreateService(chatCompletionRepo: chatCompletionRepo);
        var state = TestStateFactory.CreateTestState(conversationId: 1);

        // Act
        var results = new List<(ConversationState state, ConversationResult? result)>();
        await foreach (var item in service.TalkAsync("テスト入力", state))
        {
            results.Add(item);
        }

        // Assert
        results.Should().HaveCount(1);
        results[0].result.Should().BeNull();
    }

    [Fact]
    public async Task TalkAsync_PatienceCountが正しく更新されること()
    {
        // Arrange
        var mockFunction = new Helpers.MockToolFunction(canBeLazy: true, new MockLogger());
        var chatCompletionRepo = new MockChatCompletionRepository
        {
            CompleteChatAsyncFunc = (messages, options) =>
            {
                // 最初の呼び出し: ToolCallsを含むCompletion
                if (!messages.Any(m => m is ToolChatMessage))
                {
                    return Task.FromResult(ChatCompletionFactory.CreateToolCallsCompletionResult(
                        Kotonoha.Akane,
                        "関数を実行するで",
                        ("call_123", "MockToolFunction", "{}")
                    ));
                }
                // 2回目の呼び出し: 通常の応答
                return Task.FromResult(ChatCompletionFactory.CreateTextCompletionResult(Kotonoha.Akane, "完了したで"));
            }
        };
        var promptRepo = new MockPromptRepository();
        var service = new ConversationService(
            promptRepo,
            new MockChatMessageRepository(),
            chatCompletionRepo,
            [mockFunction],
            new MockLazyModeHandler(),
            new MockDateTimeProvider(new DateTime(2025, 1, 1, 12, 0, 0)),
            new MockLogger()
        );
        var state = TestStateFactory.CreateTestState(currentSister: Kotonoha.Akane, patienceCount: 0, conversationId: 1);

        // Act
        var results = new List<(ConversationState state, ConversationResult? result)>();
        await foreach (var item in service.TalkAsync("テスト", state))
        {
            results.Add(item);
        }

        // Assert
        results.Should().NotBeEmpty();
        var finalState = results.Last().state;

        // PatienceCountがインクリメントされていることを確認
        finalState.PatienceCount.Should().Be(1);
    }

    [Fact]
    public async Task TalkAsync_LazyModeHandlerが呼ばれること()
    {
        // Arrange
        var handlerCalled = false;
        var mockFunction = new Helpers.MockToolFunction(canBeLazy: true, new MockLogger());
        var chatCompletionRepo = new MockChatCompletionRepository
        {
            CompleteChatAsyncFunc = (messages, options) =>
            {
                // 最初の呼び出し: ToolCallsを含むCompletion
                if (!messages.Any(m => m is ToolChatMessage))
                {
                    return Task.FromResult(ChatCompletionFactory.CreateToolCallsCompletionResult(
                        Kotonoha.Akane,
                        "関数を実行するで",
                        ("call_123", "MockToolFunction", "{}")
                    ));
                }
                // 2回目の呼び出し: 通常の応答
                return Task.FromResult(ChatCompletionFactory.CreateTextCompletionResult(Kotonoha.Akane, "完了したで"));
            }
        };
        var lazyModeHandler = new MockLazyModeHandler
        {
            HandleLazyModeAsyncFunc = (completion, state, dateTime, regenerateFunc) =>
            {
                handlerCalled = true;
                var result = new LazyModeResult
                {
                    WasLazy = false,
                    LazyResponse = null,
                    FinalCompletion = completion
                };
                return Task.FromResult((result, state));
            }
        };
        var service = new ConversationService(
            new MockPromptRepository(),
            new MockChatMessageRepository(),
            chatCompletionRepo,
            [mockFunction],
            lazyModeHandler,
            new MockDateTimeProvider(new DateTime(2025, 1, 1, 12, 0, 0)),
            new MockLogger()
        );
        var state = TestStateFactory.CreateTestState(currentSister: Kotonoha.Akane, conversationId: 1);

        // Act
        var results = new List<(ConversationState state, ConversationResult? result)>();
        await foreach (var item in service.TalkAsync("テスト", state))
        {
            results.Add(item);
        }

        // Assert
        handlerCalled.Should().BeTrue("LazyModeHandler.HandleLazyModeAsync が呼ばれるべき");
    }

    [Fact]
    public async Task TalkAsync_怠け癖応答が返されること()
    {
        // Arrange
        var mockFunction = new Helpers.MockToolFunction(canBeLazy: true, new MockLogger());
        var chatCompletionRepo = new MockChatCompletionRepository
        {
            CompleteChatAsyncFunc = (messages, options) =>
            {
                // 最初の呼び出し: ToolCallsを含むCompletion
                if (!messages.Any(m => m is ToolChatMessage))
                {
                    return Task.FromResult(ChatCompletionFactory.CreateToolCallsCompletionResult(
                        Kotonoha.Akane,
                        "関数を実行するで",
                        ("call_123", "MockToolFunction", "{}")
                    ));
                }
                // 2回目の呼び出し: 怠け癖後の応答
                return Task.FromResult(ChatCompletionFactory.CreateTextCompletionResult(Kotonoha.Aoi, "わかりました、やります"));
            }
        };
        var lazyModeHandler = new MockLazyModeHandler
        {
            HandleLazyModeAsyncFunc = (completion, state, dateTime, regenerateFunc) =>
            {
                // 怠け癖モードを発動
                var lazyResponse = new ConversationResult
                {
                    Sister = Kotonoha.Akane,
                    Message = "葵、任せたで",
                    Functions = null
                };
                var lazyResult = new LazyModeResult
                {
                    WasLazy = true,
                    LazyResponse = lazyResponse,
                    FinalCompletion = ChatCompletionFactory.CreateTextCompletion(Kotonoha.Aoi, "わかりました、やります")
                };
                var newState = state.SwitchToSister(Kotonoha.Aoi, dateTime);
                return Task.FromResult((lazyResult, newState));
            }
        };
        var service = new ConversationService(
            new MockPromptRepository(),
            new MockChatMessageRepository(),
            chatCompletionRepo,
            [mockFunction],
            lazyModeHandler,
            new MockDateTimeProvider(new DateTime(2025, 1, 1, 12, 0, 0)),
            new MockLogger()
        );
        var state = TestStateFactory.CreateTestState(currentSister: Kotonoha.Akane, patienceCount: 5, conversationId: 1);

        // Act
        var results = new List<(ConversationState state, ConversationResult? result)>();
        await foreach (var item in service.TalkAsync("テスト", state))
        {
            results.Add(item);
        }

        // Assert
        results.Should().HaveCountGreaterThanOrEqualTo(2);

        // 怠け癖応答が含まれていることを確認
        var lazyResponse = results.FirstOrDefault(r => r.result?.Message == "葵、任せたで");
        lazyResponse.result.Should().NotBeNull("怠け癖応答が返されるべき");

        // PatienceCountがリセットされていることを確認
        var finalState = results.Last().state;
        finalState.PatienceCount.Should().Be(0, "怠け癖発動後はPatienceCountがリセットされるべき");
    }

    [Fact]
    public async Task TalkAsync_FinalCompletionがnullの場合_nullを返すこと()
    {
        // Arrange
        var chatCompletionRepo = new MockChatCompletionRepository
        {
            CompleteChatAsyncFunc = (messages, options) =>
                Task.FromResult(ChatCompletionFactory.CreateTextCompletionResult(Kotonoha.Akane, "テスト"))
        };
        var lazyModeHandler = new MockLazyModeHandler
        {
            HandleLazyModeAsyncFunc = (completion, state, dateTime, regenerateFunc) =>
            {
                var result = new LazyModeResult
                {
                    WasLazy = false,
                    LazyResponse = null,
                    FinalCompletion = null! // FinalCompletion を null に設定
                };
                return Task.FromResult((result, state));
            }
        };
        var service = CreateService(chatCompletionRepo: chatCompletionRepo, lazyModeHandler: lazyModeHandler);
        var state = TestStateFactory.CreateTestState(conversationId: 1);

        // Act
        var results = new List<(ConversationState state, ConversationResult? result)>();
        await foreach (var item in service.TalkAsync("こんにちは", state))
        {
            results.Add(item);
        }

        // Assert
        results.Should().HaveCount(1);
        results[0].result.Should().BeNull();
    }

    [Fact]
    public async Task TalkAsync_AssistantMessageが追加されること()
    {
        // Arrange
        var chatCompletionRepo = new MockChatCompletionRepository
        {
            CompleteChatAsyncFunc = (messages, options) =>
                Task.FromResult(ChatCompletionFactory.CreateTextCompletionResult(Kotonoha.Akane, "こんにちはやで"))
        };
        var service = CreateService(chatCompletionRepo: chatCompletionRepo);
        var state = TestStateFactory.CreateTestState(conversationId: 1);

        // Act
        var results = new List<(ConversationState state, ConversationResult? result)>();
        await foreach (var item in service.TalkAsync("こんにちは", state))
        {
            results.Add(item);
        }

        // Assert
        results.Should().NotBeEmpty();
        var finalState = results.Last().state;

        // ChatMessages に AssistantChatMessage が追加されていることを確認
        var assistantMessages = finalState.ChatMessages
            .OfType<AssistantChatMessage>()
            .ToList();
        assistantMessages.Should().NotBeEmpty();

        // 最後に追加されたアシスタントメッセージの内容を確認
        var lastAssistantMessage = assistantMessages.Last().Content.AsJson();
        lastAssistantMessage.Should().NotBeNull();
        lastAssistantMessage.Should().HavePropertyWithStringValue("Assistant", "Akane");
    }

    [Fact]
    public async Task TalkAsync_関数が実行されること()
    {
        // Arrange
        var functionInvoked = false;
        var mockFunction = new Helpers.MockToolFunction(canBeLazy: false, new MockLogger());
        var chatCompletionRepo = new MockChatCompletionRepository
        {
            CompleteChatAsyncFunc = (messages, options) =>
            {
                // 最初の呼び出し: ToolCallsを含むCompletion
                if (!messages.Any(m => m is ToolChatMessage))
                {
                    return Task.FromResult(ChatCompletionFactory.CreateToolCallsCompletionResult(
                        Kotonoha.Akane,
                        "関数を実行するで",
                        ("call_123", "MockToolFunction", "{}")
                    ));
                }
                // 2回目の呼び出し: ToolChatMessageが追加された後の応答
                functionInvoked = true;
                return Task.FromResult(ChatCompletionFactory.CreateTextCompletionResult(Kotonoha.Akane, "完了したで"));
            }
        };
        var service = new ConversationService(
            new MockPromptRepository(),
            new MockChatMessageRepository(),
            chatCompletionRepo,
            [mockFunction],
            new MockLazyModeHandler(),
            new MockDateTimeProvider(new DateTime(2025, 1, 1, 12, 0, 0)),
            new MockLogger()
        );
        var state = TestStateFactory.CreateTestState(currentSister: Kotonoha.Akane, conversationId: 1);

        // Act
        var results = new List<(ConversationState state, ConversationResult? result)>();
        await foreach (var item in service.TalkAsync("テスト", state))
        {
            results.Add(item);
        }

        // Assert
        results.Should().NotBeEmpty();
        functionInvoked.Should().BeTrue("関数が実行され、2回目のCompleteChatAsyncが呼ばれるべき");

        // ChatMessagesにToolChatMessageが追加されていることを確認
        var finalState = results.Last().state;
        var toolMessages = finalState.ChatMessages.OfType<ToolChatMessage>().ToList();
        toolMessages.Should().NotBeEmpty("関数実行結果がToolChatMessageとして追加されるべき");
    }

    [Fact]
    public async Task TalkAsync_ForgetMemory実行時_新しい会話が作成されること()
    {
        // Arrange
        var createNewConversationCalled = false;
        var chatMessageRepo = new MockChatMessageRepository
        {
            CreateNewConversationIdAsyncFunc = () =>
            {
                createNewConversationCalled = true;
                return Task.FromResult(999);
            }
        };

        // IRandomGeneratorのモック（常に成功するように設定）
        var randomGenerator = new Helpers.MockRandomGenerator(0.5); // 1/10未満でないので成功

        var forgetMemoryFunction = new ForgetMemory(new MockPromptRepository(), randomGenerator, new MockLogger());
        var chatCompletionRepo = new MockChatCompletionRepository
        {
            CompleteChatAsyncFunc = (messages, options) =>
            {
                // 最初の呼び出し: ForgetMemoryのToolCallsを含むCompletion
                if (!messages.Any(m => m is ToolChatMessage))
                {
                    return Task.FromResult(ChatCompletionFactory.CreateToolCallsCompletionResult(
                        Kotonoha.Akane,
                        "マスター、今までありがとうな…",
                        ("call_123", "ForgetMemory", "{}")
                    ));
                }
                // 2回目の呼び出し: 削除後の応答
                return Task.FromResult(ChatCompletionFactory.CreateTextCompletionResult(Kotonoha.Akane, "はじめまして"));
            }
        };

        var service = new ConversationService(
            new MockPromptRepository(),
            chatMessageRepo,
            chatCompletionRepo,
            [forgetMemoryFunction],
            new MockLazyModeHandler(),
            new MockDateTimeProvider(new DateTime(2025, 1, 1, 12, 0, 0)),
            new MockLogger()
        );
        var state = TestStateFactory.CreateTestState(currentSister: Kotonoha.Akane, conversationId: 1);

        // Act
        var results = new List<(ConversationState state, ConversationResult? result)>();
        await foreach (var item in service.TalkAsync("記憶を消して", state))
        {
            results.Add(item);
        }

        // Assert
        createNewConversationCalled.Should().BeTrue("ForgetMemory成功時にCreateNewConversationAsyncが呼ばれるべき");

        var finalState = results.Last().state;
        finalState.ConversationId.Should().Be(999, "新しい会話IDが設定されるべき");
    }

    /// <summary>
    /// テスト用のモック IRandomGenerator
    /// </summary>
    private class MockRandomGenerator : IRandomGenerator
    {
        private readonly double _returnValue;

        public MockRandomGenerator(double returnValue)
        {
            _returnValue = returnValue;
        }

        public double NextDouble() => _returnValue;
    }

    [Fact]
    public async Task TalkAsync_状態が保存されること()
    {
        // Arrange
        var insertCalled = false;
        var insertedMessages = new List<ChatMessage>();
        var chatMessageRepo = new MockChatMessageRepository
        {
            InsertChatMessagesAsyncFunc = (messages, conversationId) =>
            {
                insertCalled = true;
                insertedMessages.AddRange(messages);
                return Task.CompletedTask;
            }
        };
        var chatCompletionRepo = new MockChatCompletionRepository
        {
            CompleteChatAsyncFunc = (messages, options) =>
                Task.FromResult(ChatCompletionFactory.CreateTextCompletionResult(Kotonoha.Akane, "わかったで"))
        };
        var service = CreateService(chatMessageRepo: chatMessageRepo, chatCompletionRepo: chatCompletionRepo);
        var state = TestStateFactory.CreateTestState(conversationId: 1);

        // Act
        var results = new List<(ConversationState state, ConversationResult? result)>();
        await foreach (var item in service.TalkAsync("こんにちは", state))
        {
            results.Add(item);
        }

        // Assert
        insertCalled.Should().BeTrue("InsertChatMessagesAsync が呼ばれるべき");
        insertedMessages.Should().NotBeEmpty("未保存メッセージが保存されるべき");

        // ユーザーメッセージとアシスタントメッセージが保存されていることを確認
        insertedMessages.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task TalkAsync_最終結果が正しく返されること()
    {
        // Arrange
        var chatCompletionRepo = new MockChatCompletionRepository
        {
            CompleteChatAsyncFunc = (messages, options) =>
                Task.FromResult(ChatCompletionFactory.CreateTextCompletionResult(Kotonoha.Akane, "こんにちはやで"))
        };
        var service = CreateService(chatCompletionRepo: chatCompletionRepo);
        var state = TestStateFactory.CreateTestState(currentSister: Kotonoha.Akane, conversationId: 1);

        // Act
        var results = new List<(ConversationState state, ConversationResult? result)>();
        await foreach (var item in service.TalkAsync("こんにちは", state))
        {
            results.Add(item);
        }

        // Assert
        results.Should().NotBeEmpty();
        var finalResult = results.Last();

        // ConversationResult が返されることを確認
        finalResult.result.Should().NotBeNull();

        // Message が正しく設定されていることを確認
        finalResult.result!.Message.Should().Be("こんにちはやで");

        // Sister が正しく設定されていることを確認
        finalResult.result.Sister.Should().Be(Kotonoha.Akane);

        // Functions は null または空のリストであることを確認（ツール呼び出しなし）
        (finalResult.result.Functions == null || finalResult.result.Functions.Count == 0).Should().BeTrue();
    }

    [Fact]
    public async Task TalkAsync_ChatResponseのパースに失敗した場合_nullを返すこと()
    {
        // Arrange
        var chatCompletionRepo = new MockChatCompletionRepository
        {
            CompleteChatAsyncFunc = (messages, options) =>
                Task.FromResult(ChatCompletionFactory.CreateInvalidCompletionResult("This is not a valid JSON"))
        };
        var service = CreateService(chatCompletionRepo: chatCompletionRepo);
        var state = TestStateFactory.CreateTestState(conversationId: 1);

        // Act
        var results = new List<(ConversationState state, ConversationResult? result)>();
        await foreach (var item in service.TalkAsync("こんにちは", state))
        {
            results.Add(item);
        }

        // Assert
        results.Should().HaveCount(1);
        results[0].result.Should().BeNull();
    }

    [Fact]
    public async Task TalkAsync_複数回の関数呼び出しループが処理されること()
    {
        // Arrange
        var callCount = 0;
        var mockFunction = new Helpers.MockToolFunction(canBeLazy: false, new MockLogger());
        var chatCompletionRepo = new MockChatCompletionRepository
        {
            CompleteChatAsyncFunc = (messages, options) =>
            {
                callCount++;
                var toolMessages = messages.OfType<ToolChatMessage>().ToList();

                // 最初の呼び出し: ToolCallsを含むCompletion
                if (toolMessages.Count == 0)
                {
                    return Task.FromResult(ChatCompletionFactory.CreateToolCallsCompletionResult(
                        Kotonoha.Akane,
                        "最初の関数を実行するで",
                        ("call_1", "MockToolFunction", "{}")
                    ));
                }
                // 2回目の呼び出し: さらにToolCallsを含むCompletion
                else if (toolMessages.Count == 1)
                {
                    return Task.FromResult(ChatCompletionFactory.CreateToolCallsCompletionResult(
                        Kotonoha.Akane,
                        "次の関数も実行するで",
                        ("call_2", "MockToolFunction", "{}")
                    ));
                }
                // 3回目の呼び出し: 通常の応答（Stop）
                else
                {
                    return Task.FromResult(ChatCompletionFactory.CreateTextCompletionResult(Kotonoha.Akane, "全部完了したで"));
                }
            }
        };
        var service = new ConversationService(
            new MockPromptRepository(),
            new MockChatMessageRepository(),
            chatCompletionRepo,
            [mockFunction],
            new MockLazyModeHandler(),
            new MockDateTimeProvider(new DateTime(2025, 1, 1, 12, 0, 0)),
            new MockLogger()
        );
        var state = TestStateFactory.CreateTestState(currentSister: Kotonoha.Akane, conversationId: 1);

        // Act
        var results = new List<(ConversationState state, ConversationResult? result)>();
        await foreach (var item in service.TalkAsync("テスト", state))
        {
            results.Add(item);
        }

        // Assert
        results.Should().NotBeEmpty();

        // CompleteChatAsyncが3回呼ばれることを確認
        callCount.Should().Be(3, "関数呼び出しループが2回 + 最終応答で合計3回呼ばれるべき");

        // ChatMessagesに2つのToolChatMessageが追加されていることを確認
        var finalState = results.Last().state;
        var toolMessages = finalState.ChatMessages.OfType<ToolChatMessage>().ToList();
        toolMessages.Should().HaveCount(2, "2回の関数呼び出し結果が追加されるべき");

        // 最終的な結果が返されることを確認
        var finalResult = results.Last().result;
        finalResult.Should().NotBeNull();
        finalResult!.Message.Should().Be("全部完了したで");
    }

    #endregion

    #region TrySwitchSister テスト

    [Fact]
    public void TrySwitchSister_同じ姉妹の場合_何も変更しないこと()
    {
        // Arrange
        var service = CreateService();
        var dateTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var state = TestStateFactory.CreateTestState(currentSister: Kotonoha.Akane);

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
        var state = TestStateFactory.CreateTestState(currentSister: Kotonoha.Aoi);

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
        var state = TestStateFactory.CreateTestState(currentSister: Kotonoha.Akane);

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
        var state = TestStateFactory.CreateTestState(currentSister: Kotonoha.Aoi);

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
        var state = TestStateFactory.CreateTestState(currentSister: Kotonoha.Akane);

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
        var state = TestStateFactory.CreateTestState(currentSister: Kotonoha.Akane);

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
        var state = TestStateFactory.CreateTestState(currentSister: Kotonoha.Aoi);

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
        var state = TestStateFactory.CreateTestState(currentSister: Kotonoha.Akane);

        // Act - 姉妹名を含まない入力
        var result = service.TrySwitchSister("今日の天気は？", dateTime, state);

        // Assert
        result.Should().Be(state);
        result.CurrentSister.Should().Be(Kotonoha.Akane);
        result.ChatMessages.Should().BeEmpty();
    }

    #endregion
}
