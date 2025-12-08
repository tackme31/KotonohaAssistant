using FluentAssertions;
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
        // TODO: 空の入力の場合、何も返さないことを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_WithWhitespaceInput_ShouldReturnEmpty()
    {
        // TODO: 空白のみの入力の場合、何も返さないことを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_ShouldTrySwitchSister()
    {
        // TODO: 姉妹切り替えが試行されることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_ShouldAddUserMessage()
    {
        // TODO: ユーザーメッセージが会話に追加されることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_ShouldCallChatCompletion()
    {
        // TODO: チャット補完が呼び出されることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_ShouldHandleLazyMode()
    {
        // TODO: 怠け癖モードが処理されることを検証
        throw new NotImplementedException();
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
        // TODO: 関数が呼び出されることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_ShouldReturnConversationResult()
    {
        // TODO: 会話結果が返されることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_ShouldSaveState()
    {
        // TODO: 状態が保存されることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_WhenCompletionIsNull_ShouldReturnEmpty()
    {
        // TODO: チャット補完がnullの場合、何も返さないことを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task TalkWithKotonohaSisters_WhenForgetMemoryInvoked_ShouldCreateNewConversation()
    {
        // TODO: ForgetMemory関数が呼び出された場合、新しい会話を作成することを検証
        throw new NotImplementedException();
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
