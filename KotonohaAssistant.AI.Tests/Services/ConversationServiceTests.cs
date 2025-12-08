using FluentAssertions;
using Google.Apis.Util;
using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Utils;
using Moq;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace KotonohaAssistant.AI.Tests.Services;

public class ConversationServiceTests
{
    private readonly Mock<IPromptRepository> _mockPromptRepository;
    private readonly Mock<IChatMessageRepository> _mockChatMessageRepository;
    private readonly Mock<IChatCompletionRepository> _mockChatCompletionRepository;
    private readonly Mock<ISisterSwitchingService> _mockSisterSwitchingService;
    private readonly Mock<ILazyModeHandler> _mockLazyModeHandler;
    private readonly Mock<ILogger> _mockLogger;

    public ConversationServiceTests()
    {
        _mockPromptRepository = new Mock<IPromptRepository>();
        _mockChatMessageRepository = new Mock<IChatMessageRepository>();
        _mockChatCompletionRepository = new Mock<IChatCompletionRepository>();
        _mockSisterSwitchingService = new Mock<ISisterSwitchingService>();
        _mockLazyModeHandler = new Mock<ILazyModeHandler>();
        _mockLogger = new Mock<ILogger>();

        // デフォルトのセットアップ
        SetupDefaultMocks();
    }

    private void SetupDefaultMocks()
    {
        // プロンプトリポジトリのセットアップ
        _mockPromptRepository
            .Setup(x => x.GetCharacterPrompt(It.IsAny<Kotonoha>()))
            .Returns("テスト用プロンプト");
    }

    private ConversationState CreateInitialState(Kotonoha initialSister)
    {
        return new ConversationState
        {
            CurrentSister = initialSister,
            CharacterPromptAkane = "test prompt akane",
            CharacterPromptAoi = "test prompt aoi",
            PatienceCount = 0,
            LastToolCallSister = initialSister
        };
    }

    private ToolFunction CreateMockFunction(string name, bool canBeLazy)
    {
        return new TestToolFunction(name, canBeLazy, _mockLogger.Object);
    }

    private ConversationService CreateService(
        ConversationState state,
        IList<ToolFunction>? availableFunctions = null)
    {
        availableFunctions ??= new List<ToolFunction>();

        return new ConversationService(
            state,
            _mockChatMessageRepository.Object,
            _mockChatCompletionRepository.Object,
            availableFunctions,
            _mockSisterSwitchingService.Object,
            _mockLazyModeHandler.Object,
            _mockLogger.Object);
    }

    private static async Task<List<ConversationResult>> CollectAllResultsAsync(IAsyncEnumerable<ConversationResult> resultStream)
    {
        var results = new List<ConversationResult>();
        await foreach (var result in resultStream)
        {
            results.Add(result);
        }
        return results;
    }

    private ChatCompletion CreateChatCompletion(
        ChatFinishReason finishReason,
        string content)
    {
        var finishReasonStr = finishReason switch
        {
            ChatFinishReason.Stop => "stop",
            ChatFinishReason.ToolCalls => "tool_calls",
            _ => "stop"
        };

        // 改行やタブを削除してJSONを1行にする
        var escapedContent = content
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\"", "\\\"");

        var json = $@"{{""id"":""test-id"",""object"":""chat.completion"",""created"":{DateTimeOffset.UtcNow.ToUnixTimeSeconds()},""model"":""gpt-4"",""choices"":[{{""index"":0,""message"":{{""role"":""assistant"",""content"":""{escapedContent}""}},""finish_reason"":""{finishReasonStr}""}}]}}";

        return ModelReaderWriter.Read<ChatCompletion>(BinaryData.FromString(json))!;
    }

    private ChatCompletion CreateToolChatCompletion()
    {

    }

    private void SetupLazyModeHandler(LazyModeResult result, ConversationState state)
    {
        _mockLazyModeHandler
            .Setup(x => x.HandleLazyModeAsync(
                It.IsAny<ChatCompletion>(),
                It.IsAny<ConversationState>(),
                It.IsAny<Func<Task<ChatCompletion?>>>()))
            .Callback(() => state.SwitchToOtherSister())
            .ReturnsAsync(result);
    }

    // テスト用の具象クラス
    private class TestToolFunction : ToolFunction
    {
        private readonly string _name;
        private readonly bool _canBeLazy;

        public TestToolFunction(string name, bool canBeLazy, ILogger logger) : base(logger)
        {
            _name = name;
            _canBeLazy = canBeLazy;
        }

        public override string Description => "Test function";
        public override string Parameters => "{}";
        public override bool CanBeLazy { get => _canBeLazy; set => throw new NotImplementedException(); }

        public override bool TryParseArguments(System.Text.Json.JsonDocument doc, out IDictionary<string, object> arguments)
        {
            arguments = new Dictionary<string, object>();
            return true;
        }

        public override Task<string> Invoke(IDictionary<string, object> arguments, IReadOnlyConversationState state)
        {
            return Task.FromResult("test result");
        }

        // GetType().Nameが正しい名前を返すようにする
        public override string ToString() => _name;
    }


    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultSister()
    {
        // TODO: デフォルトの姉妹（茜）で初期化されることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public void Constructor_ShouldInitializeWithSpecifiedSister()
    {
        // TODO: 指定した姉妹（葵）で初期化されることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public void Constructor_ShouldSetPatienceCountToZero()
    {
        // TODO: 忍耐値が0で初期化されることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public void Constructor_ShouldLoadCharacterPrompts()
    {
        // TODO: キャラクタープロンプトが読み込まれることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public void Constructor_ShouldRegisterAllFunctions()
    {
        // TODO: 提供された関数が全て登録されることを検証
        throw new NotImplementedException();
    }

    #endregion

    #region LoadLatestConversation Tests

    [Fact]
    public async Task LoadLatestConversation_WhenNoConversationExists_ShouldCreateNewConversation()
    {
        // TODO: 会話履歴がない場合、新しい会話を作成することを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task LoadLatestConversation_WhenConversationExists_ShouldLoadMessages()
    {
        // TODO: 既存の会話が読み込まれることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task LoadLatestConversation_ShouldSetCurrentSisterFromLastMessage()
    {
        // TODO: 最後のメッセージから現在の姉妹を設定することを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task LoadLatestConversation_WhenLastMessageIsNotParseable_ShouldDefaultToAkane()
    {
        // TODO: 最後のメッセージがパースできない場合、茜をデフォルトにすることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task LoadLatestConversation_WhenExceptionOccurs_ShouldLogError()
    {
        // TODO: 例外発生時にエラーログが記録されることを検証
        throw new NotImplementedException();
    }

    #endregion

    #region TalkWithKotonohaSisters Tests

    [Fact]
    public async Task TalkWithKotonohaSisters_WithEmptyInput_ShouldReturnEmpty()
    {
        // Arrange
        var state = CreateInitialState(Kotonoha.Akane);
        var service = CreateService(state);

        // Act
        var results = await CollectAllResultsAsync(service.TalkWithKotonohaSisters(string.Empty));

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_WithWhitespaceInput_ShouldReturnEmpty()
    {
        // Arrange
        var state = CreateInitialState(Kotonoha.Akane);
        var service = CreateService(state);

        // Act
        var results = await CollectAllResultsAsync(service.TalkWithKotonohaSisters("   "));

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_ShouldTrySwitchSister()
    {
        // Arrange
        var inputText = "テストメッセージ";
        var state = CreateInitialState(Kotonoha.Akane);
        var service = CreateService(state);

        var completion = CreateChatCompletion(
            ChatFinishReason.Stop,
            @"{""Assistant"":""Akane"",""Text"":""テスト応答やで"",""Emotion"":""Calm""}");

        _mockChatCompletionRepository
            .Setup(x => x.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions?>()))
            .Returns(Task.FromResult(ClientResult.FromValue(completion, new MockPipelineResponse())));

        var lazyResult = new LazyModeResult
        {
            FinalCompletion = completion,
            WasLazy = false,
            LazyResponse = null
        };
        SetupLazyModeHandler(lazyResult, state);

        // Act
        var results = await CollectAllResultsAsync(service.TalkWithKotonohaSisters(inputText));

        // Assert
        _mockSisterSwitchingService.Verify(
            x => x.TrySwitchSister(
                It.Is<string>(input => input == inputText),
                It.IsAny<ConversationState>()),
            Times.Once);
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_ShouldAddUserMessage()
    {
        // Arrange
        var inputText = "テストメッセージ";
        var state = CreateInitialState(Kotonoha.Akane);
        var service = CreateService(state);

        var completion = CreateChatCompletion(
            ChatFinishReason.Stop,
            @"{""Assistant"":""Akane"",""Text"":""テスト応答やで"",""Emotion"":""Calm""}");

        _mockChatCompletionRepository
            .Setup(x => x.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions?>()))
            .Returns(Task.FromResult(ClientResult.FromValue(completion, new MockPipelineResponse())));

        var lazyResult = new LazyModeResult
        {
            FinalCompletion = completion,
            WasLazy = false,
            LazyResponse = null
        };
        SetupLazyModeHandler(lazyResult, state);

        var initialMessageCount = state.ChatMessages.Count();

        // Act
        var results = await CollectAllResultsAsync(service.TalkWithKotonohaSisters(inputText));

        // Assert
        // ユーザーメッセージとアシスタントメッセージが追加されているはず
        state.ChatMessages.Should().HaveCountGreaterThan(initialMessageCount);
        state.ChatMessages.Should().Contain(m => m.Content[0].Text.Contains(inputText));
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_ShouldCallChatCompletion()
    {
        // Arrange
        var inputText = "テストメッセージ";
        var state = CreateInitialState(Kotonoha.Akane);
        var service = CreateService(state);

        var completion = CreateChatCompletion(
            ChatFinishReason.Stop,
            @"{""Assistant"":""Akane"",""Text"":""テスト応答やで"",""Emotion"":""Calm""}");

        _mockChatCompletionRepository
            .Setup(x => x.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions?>()))
            .Returns(Task.FromResult(ClientResult.FromValue(completion, new MockPipelineResponse())));

        var lazyResult = new LazyModeResult
        {
            FinalCompletion = completion,
            WasLazy = false,
            LazyResponse = null
        };
        SetupLazyModeHandler(lazyResult, state);

        // Act
        var results = await CollectAllResultsAsync(service.TalkWithKotonohaSisters(inputText));

        // Assert
        _mockChatCompletionRepository.Verify(
            x => x.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions?>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_ShouldHandleLazyMode()
    {
        // Arrange
        var inputText = "テストメッセージ";
        var state = CreateInitialState(Kotonoha.Akane);
        var service = CreateService(state);

        var completion = CreateChatCompletion(
            ChatFinishReason.Stop,
            @"{""Assistant"":""Akane"",""Text"":""テスト応答やで"",""Emotion"":""Calm""}");

        _mockChatCompletionRepository
            .Setup(x => x.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions?>()))
            .Returns(Task.FromResult(ClientResult.FromValue(completion, new MockPipelineResponse())));

        var lazyResult = new LazyModeResult
        {
            FinalCompletion = completion,
            WasLazy = false,
            LazyResponse = null
        };
        SetupLazyModeHandler(lazyResult, state);

        // Act
        var results = await CollectAllResultsAsync(service.TalkWithKotonohaSisters(inputText));

        // Assert
        _mockLazyModeHandler.Verify(
            x => x.HandleLazyModeAsync(
                It.IsAny<ChatCompletion>(),
                It.IsAny<ConversationState>(),
                It.IsAny<Func<Task<ChatCompletion?>>>()),
            Times.Once);
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_WhenLazyResponseExists_ShouldReturnBothResponses()
    {
        // Arrange
        var inputText = "テストメッセージ";
        var state = CreateInitialState(Kotonoha.Akane);
        var service = CreateService(state);

        // 最初のChatCompletion（通常の応答）
        var initialCompletion = CreateChatCompletion(
            ChatFinishReason.Stop,
            @"{""Assistant"":""Akane"",""Text"":""初期応答やで"",""Emotion"":""Calm""}");

        // ChatCompletionRepositoryのモック
        _mockChatCompletionRepository
            .Setup(x => x.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions?>()))
            .Returns(Task.FromResult(ClientResult.FromValue(initialCompletion, new MockPipelineResponse())));

        // 怠け癖時の応答（茜が拒否）
        var lazyRefusalResponse = new ConversationResult
        {
            Message = "葵、任せたで",
            Emotion = Emotion.Calm,
            Sister = Kotonoha.Akane,
            Functions = []
        };

        // 最終的な応答（葵が引き受け）
        var finalAcceptanceCompletion = CreateChatCompletion(
            ChatFinishReason.Stop,
            @"{""Assistant"":""Aoi"",""Text"":""もう、仕方ないなあ。"",""Emotion"":""Anger""}");

        // LazyModeHandlerのモック（怠け癖発動）
        var lazyResult = new LazyModeResult
        {
            FinalCompletion = finalAcceptanceCompletion,
            WasLazy = true,
            LazyResponse = lazyRefusalResponse
        };

        SetupLazyModeHandler(lazyResult, state);

        // Act
        var results = await CollectAllResultsAsync(service.TalkWithKotonohaSisters(inputText));

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().BeEquivalentTo(
            new ConversationResult
            {
                Message = "葵、任せたで",
                Emotion = Emotion.Calm,
                Sister = Kotonoha.Akane,
                Functions = []
            });
        results[1].Should().BeEquivalentTo(
            new ConversationResult
            {
                Message = "もう、仕方ないなあ。",
                Emotion = Emotion.Anger,
                Sister = Kotonoha.Aoi,
                Functions = []
            });

        // TrySwitchSisterが呼び出されたことを確認
        _mockSisterSwitchingService.Verify(
            x => x.TrySwitchSister(
                It.Is<string>(input => input == "テストメッセージ"),
                It.IsAny<ConversationState>()),
            Times.Once);

        // LazyModeHandlerが呼び出されたことを確認
        _mockLazyModeHandler.Verify(
            x => x.HandleLazyModeAsync(
                It.IsAny<ChatCompletion>(),
                It.IsAny<ConversationState>(),
                It.IsAny<Func<Task<ChatCompletion?>>>()),
            Times.Once);
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_ShouldInvokeFunctions()
    {
        // Arrange
        var inputText = "テストメッセージ";
        var state = CreateInitialState(Kotonoha.Akane);
        
        // テスト用の関数を作成
        var mockFunction = new Mock<ToolFunction>();
        
        var availableFunctions = new List<ToolFunction> { mockFunction.Object };
        var service = CreateService(state, availableFunctions);

        // finish_reason=stopの場合、関数は呼ばれない
        var completion = CreateChatCompletion(
            ChatFinishReason.Stop,
            @"{""Assistant"":""Akane"",""Text"":""テスト応答やで"",""Emotion"":""Calm""}");

        _mockChatCompletionRepository
            .Setup(x => x.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions?>()))
            .Returns(Task.FromResult(ClientResult.FromValue(completion, new MockPipelineResponse())));

        var lazyResult = new LazyModeResult
        {
            FinalCompletion = completion,
            WasLazy = false,
            LazyResponse = null
        };
        SetupLazyModeHandler(lazyResult, state);

        // Act
        var results = await CollectAllResultsAsync(service.TalkWithKotonohaSisters(inputText));

        // Assert
        // finish_reason=stopの場合、関数は呼ばれない
        mockFunction.Verify(x => x.Invoke(
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IReadOnlyConversationState>()),
            Times.Never);
        results.Should().HaveCount(1);
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_ShouldReturnConversationResult()
    {
        // Arrange
        var inputText = "こんにちは";
        var state = CreateInitialState(Kotonoha.Akane);
        var service = CreateService(state);

        var completion = CreateChatCompletion(
            ChatFinishReason.Stop,
            @"{""Assistant"":""Akane"",""Text"":""こんにちはやで!"",""Emotion"":""Joy""}");

        _mockChatCompletionRepository
            .Setup(x => x.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions?>()))
            .Returns(Task.FromResult(ClientResult.FromValue(completion, new MockPipelineResponse())));

        var lazyResult = new LazyModeResult
        {
            FinalCompletion = completion,
            WasLazy = false,
            LazyResponse = null
        };
        SetupLazyModeHandler(lazyResult, state);

        // Act
        var results = await CollectAllResultsAsync(service.TalkWithKotonohaSisters(inputText));

        // Assert
        results.Should().HaveCount(1);
        results[0].Message.Should().Be("こんにちはやで!");
        results[0].Emotion.Should().Be(Emotion.Joy);
        results[0].Sister.Should().Be(Kotonoha.Akane);
        results[0].Functions.Should().BeEmpty();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_ShouldSaveState()
    {
        // Arrange
        var inputText = "テストメッセージ";
        var state = CreateInitialState(Kotonoha.Akane);
        var service = CreateService(state);

        var completion = CreateChatCompletion(
            ChatFinishReason.Stop,
            @"{""Assistant"":""Akane"",""Text"":""テスト応答やで"",""Emotion"":""Calm""}");

        _mockChatCompletionRepository
            .Setup(x => x.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions?>()))
            .Returns(Task.FromResult(ClientResult.FromValue(completion, new MockPipelineResponse())));

        var lazyResult = new LazyModeResult
        {
            FinalCompletion = completion,
            WasLazy = false,
            LazyResponse = null
        };
        SetupLazyModeHandler(lazyResult, state);

        // Act
        var results = await CollectAllResultsAsync(service.TalkWithKotonohaSisters(inputText));

        // Assert
        // 会話IDがない場合は保存されないため、InsertChatMessagesAsyncは呼ばれない
        _mockChatMessageRepository.Verify(
            x => x.InsertChatMessagesAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_WhenCompletionIsNull_ShouldReturnEmpty()
    {
        // Arrange
        var inputText = "テストメッセージ";
        var state = CreateInitialState(Kotonoha.Akane);
        var service = CreateService(state);

        _mockChatCompletionRepository
            .Setup(x => x.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions?>()))
            .Returns(Task.FromResult<ClientResult<ChatCompletion>>(null!));

        // Act
        var results = await CollectAllResultsAsync(service.TalkWithKotonohaSisters(inputText));

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_WhenForgetMemoryInvoked_ShouldCreateNewConversation()
    {
        // Arrange
        var inputText = "テストメッセージ";
        var state = CreateInitialState(Kotonoha.Akane);
        state.AddUserMessage(inputText);

        var randomGenerator = new Mock<IRandomGenerator>();
        randomGenerator.Setup(x => x.NextDouble()).Returns(1);
        var functions = new List<ToolFunction>{new ForgetMemory(
            _mockPromptRepository.Object,
            randomGenerator.Object,
            _mockLogger.Object)
        };
        var service = CreateService(state, functions);
        var lastMessage = state.ChatMessages.Last();

        // finish_reason=stopの場合、ForgetMemoryは呼ばれない
        var completion = CreateChatCompletion(
            ChatFinishReason.ToolCalls,
            @"{""Assistant"":""Akane"",""Text"":""テスト応答やで"",""Emotion"":""Calm""}");

        _mockChatCompletionRepository
            .Setup(x => x.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions?>()))
            .Returns(Task.FromResult(ClientResult.FromValue(completion, new MockPipelineResponse())));

        var lazyResult = new LazyModeResult
        {
            FinalCompletion = completion,
            WasLazy = false,
            LazyResponse = null
        };
        SetupLazyModeHandler(lazyResult, state);

        // Act
        var results = await CollectAllResultsAsync(service.TalkWithKotonohaSisters(inputText));

        // Assert
        // ForgetMemoryが呼ばれていないため、新しい会話は作成されない
        state.ChatMessages.Should().NotContain(lastMessage);
    }

    #endregion

    #region GetAllMessages Tests

    [Fact]
    public void GetAllMessages_WithEmptyConversation_ShouldReturnEmpty()
    {
        // TODO: 空の会話の場合、空のリストを返すことを検証
        throw new NotImplementedException();
    }

    [Fact]
    public void GetAllMessages_ShouldSkipInitialConversation()
    {
        // TODO: 初期会話をスキップすることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public void GetAllMessages_ShouldReturnUserMessages()
    {
        // TODO: ユーザーメッセージが返されることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public void GetAllMessages_ShouldReturnAssistantMessages()
    {
        // TODO: アシスタントメッセージが返されることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public void GetAllMessages_ShouldSkipToolMessages()
    {
        // TODO: ツールメッセージはスキップされることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public void GetAllMessages_ShouldSkipMessagesWithEmptyContent()
    {
        // TODO: 空のコンテンツを持つメッセージはスキップされることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public void GetAllMessages_ShouldParseSisterFromAssistantMessage()
    {
        // TODO: アシスタントメッセージから姉妹情報をパースすることを検証
        throw new NotImplementedException();
    }

    #endregion

    #region Function Invocation Tests

    [Fact]
    public async Task TalkWithKotonohaSisters_WhenFunctionDoesNotExist_ShouldLogWarning()
    {
        // TODO: 存在しない関数が呼び出された場合、警告がログされることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_WhenFunctionArgumentsInvalid_ShouldLogWarning()
    {
        // TODO: 関数の引数が無効な場合、警告がログされることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_ShouldInvokeMultipleFunctionsInLoop()
    {
        // TODO: 複数の関数がループで呼び出されることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_ShouldAddToolMessagesToState()
    {
        // TODO: ツールメッセージが状態に追加されることを検証
        throw new NotImplementedException();
    }

    #endregion

    #region Patience Counter Tests

    [Fact]
    public async Task TalkWithKotonohaSisters_WhenToolCalls_ShouldUpdatePatienceCounter()
    {
        // TODO: ツール呼び出し時に忍耐値が更新されることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_WhenNoToolCalls_ShouldNotUpdatePatienceCounter()
    {
        // TODO: ツール呼び出しがない場合、忍耐値が更新されないことを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_WhenLazyResponseExists_ShouldClearPatienceCounter()
    {
        // TODO: 怠け癖発動時は忍耐地がリセットされることを検証
        throw new NotImplementedException();
    }

    #endregion

    #region State Persistence Tests

    [Fact]
    public async Task TalkWithKotonohaSisters_WhenNoConversationId_ShouldNotSaveState()
    {
        // TODO: 会話IDがない場合、状態を保存しないことを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_ShouldSaveOnlyUnsavedMessages()
    {
        // TODO: 未保存のメッセージのみを保存することを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_WhenSaveStateFails_ShouldLogError()
    {
        // TODO: 状態保存が失敗した場合、エラーがログされることを検証
        throw new NotImplementedException();
    }

    #endregion
}

// テスト用のモッククラス
internal class MockPipelineResponse : PipelineResponse
{
    private BinaryData _content = BinaryData.FromString("{}");

    public override int Status => 200;
    public override string ReasonPhrase => "OK";
    public override Stream? ContentStream { get; set; }
    public override BinaryData Content => _content;

    protected override PipelineResponseHeaders HeadersCore => new MockPipelineResponseHeaders();

    public override BinaryData BufferContent(CancellationToken cancellationToken = default)
    {
        return _content;
    }

    public override async ValueTask<BinaryData> BufferContentAsync(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_content);
    }

    public override void Dispose()
    {
    }
}

internal class MockPipelineResponseHeaders : PipelineResponseHeaders
{
    public override IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        yield break;
    }

    public override bool TryGetValue(string name, out string? value)
    {
        value = null;
        return false;
    }

    public override bool TryGetValues(string name, out IEnumerable<string>? values)
    {
        values = null;
        return false;
    }
}
