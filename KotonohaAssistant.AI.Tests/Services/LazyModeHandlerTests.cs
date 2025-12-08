using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Utils;
using Moq;
using OpenAI.Chat;
using System.ClientModel.Primitives;

namespace KotonohaAssistant.AI.Tests.Services;

public class LazyModeHandlerTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Dictionary<string, ToolFunction> _functions;

    public LazyModeHandlerTests()
    {
        _mockLogger = new Mock<ILogger>();
        _functions = new Dictionary<string, ToolFunction>();
    }

    private LazyModeHandler CreateHandler()
    {
        return new LazyModeHandler(_functions, _mockLogger.Object);
    }

    private ConversationState CreateState(Kotonoha initialSister)
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

    private ChatCompletion CreateChatCompletion(
        ChatFinishReason finishReason,
        string? content = null,
        IEnumerable<ChatToolCall>? toolCalls = null)
    {
        // JSON文字列から直接デシリアライズ
        var finishReasonStr = finishReason switch
        {
            ChatFinishReason.Stop => "stop",
            ChatFinishReason.ToolCalls => "tool_calls",
            _ => "stop"
        };

        var toolCallsJson = toolCalls != null && toolCalls.Any()
            ? string.Join(",", toolCalls.Select((tc, idx) => $@"
                {{
                    ""id"": ""call_{idx}"",
                    ""type"": ""function"",
                    ""function"": {{
                        ""name"": ""{tc.FunctionName}"",
                        ""arguments"": ""{tc.FunctionArguments.ToString().Replace("\"", "\\\"")}""
                    }}
                }}"))
            : null;

        var messageContent = content ?? "test";
        var toolCallsField = toolCallsJson != null ? $@",""tool_calls"": [{toolCallsJson}]" : "";

        var json = $@"{{
            ""id"": ""test-id"",
            ""object"": ""chat.completion"",
            ""created"": {DateTimeOffset.UtcNow.ToUnixTimeSeconds()},
            ""model"": ""gpt-4"",
            ""choices"": [
                {{
                    ""index"": 0,
                    ""message"": {{
                        ""role"": ""assistant"",
                        ""content"": ""{messageContent.Replace("\"", "\\\"")}""
                        {toolCallsField}
                    }},
                    ""finish_reason"": ""{finishReasonStr}""
                }}
            ]
        }}";

        return ModelReaderWriter.Read<ChatCompletion>(BinaryData.FromString(json))!;
    }

    private ChatToolCall CreateToolCall(string functionName, string arguments = "{}")
    {
        var json = $@"{{
            ""id"": ""call_{Guid.NewGuid():N}"",
            ""type"": ""function"",
            ""function"": {{
                ""name"": ""{functionName}"",
                ""arguments"": ""{arguments.Replace("\"", "\\\"")}""
            }}
        }}";

        return ModelReaderWriter.Read<ChatToolCall>(BinaryData.FromString(json))!;
    }

    private ToolFunction CreateMockFunction(string name, bool canBeLazy)
    {
        return new TestToolFunction(name, canBeLazy, _mockLogger.Object);
    }

    // テスト用の決定的な乱数ジェネレーター
    private class DeterministicRandomGenerator : IRandomGenerator
    {
        private readonly Queue<double> _values;

        public DeterministicRandomGenerator(params double[] values)
        {
            _values = new Queue<double>(values);
        }

        public double NextDouble()
        {
            return _values.Count > 0 ? _values.Dequeue() : 0.0;
        }
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

    #region HandleLazyModeAsync Tests

    [Fact]
    public async Task HandleLazyModeAsync_WhenNotLazy_ShouldReturnOriginalCompletion()
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateState(Kotonoha.Akane);
        var completion = CreateChatCompletion(ChatFinishReason.Stop, "test response");

        // Act
        var result = await handler.HandleLazyModeAsync(
            completion,
            state,
            () => Task.FromResult<ChatCompletion?>(null));

        // Assert
        Assert.False(result.WasLazy);
        Assert.Equal(completion, result.FinalCompletion);
        Assert.Null(result.LazyResponse);
    }

    [Fact]
    public async Task HandleLazyModeAsync_WhenLazy_ShouldAddLazyModeInstruction()
    {
        // Arrange
        var lazyFunc = CreateMockFunction("lazyFunc", canBeLazy: true);
        _functions["lazyFunc"] = lazyFunc;

        var handler = CreateHandler();
        var state = CreateState(Kotonoha.Akane);
        state.PatienceCount = 4;

        var toolCall = CreateToolCall("lazyFunc");
        var completion = CreateChatCompletion(ChatFinishReason.ToolCalls, null, [toolCall]);

        var refusalCompletion = CreateChatCompletion(
            ChatFinishReason.Stop,
            "{\"Assistant\": \"Akane\", \"Text\": \"葵、任せたで\", \"Emotion\": \"Calm\"}");
        var acceptanceCompletion = CreateChatCompletion(
            ChatFinishReason.ToolCalls,
            null,
            [toolCall]);

        var callCount = 0;
        var messageCountBeforeLazy = state.ChatMessages.Count();

        // Act
        await handler.HandleLazyModeAsync(
            completion,
            state,
            () => Task.FromResult<ChatCompletion?>(
                ++callCount == 1 ? refusalCompletion : acceptanceCompletion));

        // Assert: メッセージが追加されていることを確認
        Assert.True(state.ChatMessages.Count() > messageCountBeforeLazy);
    }

    [Fact]
    public async Task HandleLazyModeAsync_WhenLazy_ShouldGenerateRefusalResponse()
    {
        // Arrange
        var lazyFunc = CreateMockFunction("lazyFunc", canBeLazy: true);
        _functions["lazyFunc"] = lazyFunc;

        var handler = CreateHandler();
        var state = CreateState(Kotonoha.Akane);
        state.PatienceCount = 4;

        var toolCall = CreateToolCall("lazyFunc");
        var completion = CreateChatCompletion(ChatFinishReason.ToolCalls, null, [toolCall]);

        var refusalCompletion = CreateChatCompletion(
            ChatFinishReason.Stop,
            "{\"Assistant\": \"Akane\", \"Text\": \"葵、任せたで\", \"Emotion\": \"Calm\"}");
        var acceptanceCompletion = CreateChatCompletion(
            ChatFinishReason.ToolCalls,
            null,
            [toolCall]);

        var callCount = 0;

        // Act
        var result = await handler.HandleLazyModeAsync(
            completion,
            state,
            () => Task.FromResult<ChatCompletion?>(
                ++callCount == 1 ? refusalCompletion : acceptanceCompletion));

        // Assert: 拒否応答が生成されている
        Assert.True(result.WasLazy);
        Assert.NotNull(result.LazyResponse);
        Assert.Equal("葵、任せたで", result.LazyResponse.Message);
    }

    [Fact]
    public async Task HandleLazyModeAsync_WhenRefusalStillContainsToolCalls_ShouldCancel()
    {
        // Arrange
        var lazyFunc = CreateMockFunction("lazyFunc", canBeLazy: true);
        _functions["lazyFunc"] = lazyFunc;

        var handler = CreateHandler();
        var state = CreateState(Kotonoha.Akane);
        state.PatienceCount = 4;

        var toolCall = CreateToolCall("lazyFunc");
        var completion = CreateChatCompletion(ChatFinishReason.ToolCalls, null, [toolCall]);

        // 拒否応答でもツール呼び出しがある（異常ケース）
        var refusalWithToolCalls = CreateChatCompletion(
            ChatFinishReason.ToolCalls,
            null,
            [toolCall]);

        // Act
        var result = await handler.HandleLazyModeAsync(
            completion,
            state,
            () => Task.FromResult<ChatCompletion?>(refusalWithToolCalls));

        // Assert: キャンセルされて元の完了を返す
        Assert.False(result.WasLazy);
        Assert.Equal(completion, result.FinalCompletion);
    }

    [Fact]
    public async Task HandleLazyModeAsync_WhenLazy_ShouldSwitchSister()
    {
        // Arrange
        var lazyFunc = CreateMockFunction("lazyFunc", canBeLazy: true);
        _functions["lazyFunc"] = lazyFunc;

        var handler = CreateHandler();
        var state = CreateState(Kotonoha.Akane);
        state.PatienceCount = 4;
        var initialSister = state.CurrentSister;

        var toolCall = CreateToolCall("lazyFunc");
        var completion = CreateChatCompletion(ChatFinishReason.ToolCalls, null, [toolCall]);

        var refusalCompletion = CreateChatCompletion(
            ChatFinishReason.Stop,
            "{\"Assistant\": \"Akane\", \"Text\": \"葵、任せたで\", \"Emotion\": \"Calm\"}");
        var acceptanceCompletion = CreateChatCompletion(
            ChatFinishReason.ToolCalls,
            null,
            [toolCall]);

        var callCount = 0;

        // Act
        await handler.HandleLazyModeAsync(
            completion,
            state,
            () => Task.FromResult<ChatCompletion?>(
                ++callCount == 1 ? refusalCompletion : acceptanceCompletion));

        // Assert: 姉妹が切り替わっている
        Assert.NotEqual(initialSister, state.CurrentSister);
    }

    [Fact]
    public async Task HandleLazyModeAsync_WhenLazy_ShouldGenerateAcceptanceResponse()
    {
        // Arrange
        var lazyFunc = CreateMockFunction("lazyFunc", canBeLazy: true);
        _functions["lazyFunc"] = lazyFunc;

        var handler = CreateHandler();
        var state = CreateState(Kotonoha.Akane);
        state.PatienceCount = 4;

        var toolCall = CreateToolCall("lazyFunc");
        var completion = CreateChatCompletion(ChatFinishReason.ToolCalls, null, [toolCall]);

        var refusalCompletion = CreateChatCompletion(
            ChatFinishReason.Stop,
            "{\"Assistant\": \"Akane\", \"Text\": \"葵、任せたで\", \"Emotion\": \"Calm\"}");
        var acceptanceCompletion = CreateChatCompletion(
            ChatFinishReason.ToolCalls,
            null,
            [toolCall]);

        var callCount = 0;

        // Act
        var result = await handler.HandleLazyModeAsync(
            completion,
            state,
            () => Task.FromResult<ChatCompletion?>(
                ++callCount == 1 ? refusalCompletion : acceptanceCompletion));

        // Assert: 引き受け応答が生成されている
        Assert.True(result.WasLazy);
        Assert.Equal(acceptanceCompletion, result.FinalCompletion);
    }

    [Fact]
    public async Task HandleLazyModeAsync_WhenAcceptanceGenerationFails_ShouldReturnOriginal()
    {
        // Arrange
        var lazyFunc = CreateMockFunction("lazyFunc", canBeLazy: true);
        _functions["lazyFunc"] = lazyFunc;

        var handler = CreateHandler();
        var state = CreateState(Kotonoha.Akane);
        state.PatienceCount = 4;

        var toolCall = CreateToolCall("lazyFunc");
        var completion = CreateChatCompletion(ChatFinishReason.ToolCalls, null, [toolCall]);

        var refusalCompletion = CreateChatCompletion(
            ChatFinishReason.Stop,
            "{\"Assistant\": \"Akane\", \"Text\": \"葵、任せたで\", \"Emotion\": \"Calm\"}");

        var callCount = 0;

        // Act
        var result = await handler.HandleLazyModeAsync(
            completion,
            state,
            () =>
            {
                callCount++;
                // 2回目の呼び出し（引き受け応答）でnullを返す
                return Task.FromResult<ChatCompletion?>(
                    callCount == 1 ? refusalCompletion : null);
            });

        // Assert: 元の完了を返す
        Assert.False(result.WasLazy);
        Assert.Equal(completion, result.FinalCompletion);
    }

    #endregion

    #region ShouldBeLazy Tests

    [Fact]
    public async Task HandleLazyModeAsync_WhenFinishReasonIsNotToolCalls_ShouldNotBeLazy()
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateState(Kotonoha.Akane);
        var completion = CreateChatCompletion(ChatFinishReason.Stop, "normal response");

        // Act
        var result = await handler.HandleLazyModeAsync(
            completion,
            state,
            () => Task.FromResult<ChatCompletion?>(null));

        // Assert
        Assert.False(result.WasLazy);
    }

    [Fact]
    public async Task HandleLazyModeAsync_WhenFunctionCannotBeLazy_ShouldNotBeLazy()
    {
        // Arrange
        var nonLazyFunc = CreateMockFunction("nonLazyFunc", canBeLazy: false);
        _functions["nonLazyFunc"] = nonLazyFunc;

        var handler = CreateHandler();
        var state = CreateState(Kotonoha.Akane);
        var toolCall = CreateToolCall("nonLazyFunc");
        var completion = CreateChatCompletion(ChatFinishReason.ToolCalls, null, [toolCall]);

        // Act
        var result = await handler.HandleLazyModeAsync(
            completion,
            state,
            () => Task.FromResult<ChatCompletion?>(null));

        // Assert
        Assert.False(result.WasLazy);
    }

    [Fact]
    public async Task HandleLazyModeAsync_WhenPatienceCountExceeds3_ShouldBeLazy()
    {
        // Arrange
        var lazyFunc = CreateMockFunction("lazyFunc", canBeLazy: true);
        _functions["lazyFunc"] = lazyFunc;

        var handler = CreateHandler();
        var state = CreateState(Kotonoha.Akane);
        state.PatienceCount = 4; // Exceeds 3

        var toolCall = CreateToolCall("lazyFunc");
        var completion = CreateChatCompletion(ChatFinishReason.ToolCalls, null, [toolCall]);

        var refusalCompletion = CreateChatCompletion(
            ChatFinishReason.Stop,
            "{\"Assistant\": \"Akane\", \"Text\": \"葵、任せたで\", \"Emotion\": \"Calm\"}");
        var acceptanceCompletion = CreateChatCompletion(
            ChatFinishReason.ToolCalls,
            null,
            [toolCall]);

        var callCount = 0;
        Func<Task<ChatCompletion?>> regenerate = () =>
        {
            callCount++;
            return Task.FromResult<ChatCompletion?>(
                callCount == 1 ? refusalCompletion : acceptanceCompletion);
        };

        // Act
        var result = await handler.HandleLazyModeAsync(completion, state, regenerate);

        // Assert
        Assert.True(result.WasLazy);
        Assert.Equal(acceptanceCompletion, result.FinalCompletion);
        Assert.NotNull(result.LazyResponse);
    }

    [Fact]
    public async Task HandleLazyModeAsync_RandomProbability_ShouldBeLazy_WhenUnderThreshold()
    {
        // Arrange
        var lazyFunc = CreateMockFunction("lazyFunc", canBeLazy: true);
        _functions["lazyFunc"] = lazyFunc;

        // 0.09 < 0.1 なので怠ける
        var deterministicRandom = new DeterministicRandomGenerator(0.09);
        var handler = new LazyModeHandler(_functions, _mockLogger.Object, deterministicRandom);

        var state = CreateState(Kotonoha.Akane);
        state.PatienceCount = 1; // Low patience count

        var toolCall = CreateToolCall("lazyFunc");
        var completion = CreateChatCompletion(ChatFinishReason.ToolCalls, null, [toolCall]);

        var refusalCompletion = CreateChatCompletion(
            ChatFinishReason.Stop,
            "{\"Assistant\": \"Akane\", \"Text\": \"葵、任せたで\", \"Emotion\": \"Calm\"}");
        var acceptanceCompletion = CreateChatCompletion(
            ChatFinishReason.ToolCalls,
            null,
            [toolCall]);

        var callCount = 0;

        // Act
        var result = await handler.HandleLazyModeAsync(
            completion,
            state,
            () => Task.FromResult<ChatCompletion?>(
                ++callCount == 1 ? refusalCompletion : acceptanceCompletion));

        // Assert
        Assert.True(result.WasLazy);
        Assert.NotNull(result.LazyResponse);
        Assert.Equal("葵、任せたで", result.LazyResponse.Message);
    }

    [Fact]
    public async Task HandleLazyModeAsync_RandomProbability_ShouldNotBeLazy_WhenOverThreshold()
    {
        // Arrange
        var lazyFunc = CreateMockFunction("lazyFunc", canBeLazy: true);
        _functions["lazyFunc"] = lazyFunc;

        // 0.11 >= 0.1 なので怠けない
        var deterministicRandom = new DeterministicRandomGenerator(0.11);
        var handler = new LazyModeHandler(_functions, _mockLogger.Object, deterministicRandom);

        var state = CreateState(Kotonoha.Akane);
        state.PatienceCount = 1; // Low patience count

        var toolCall = CreateToolCall("lazyFunc");
        var completion = CreateChatCompletion(ChatFinishReason.ToolCalls, null, [toolCall]);

        // Act
        var result = await handler.HandleLazyModeAsync(
            completion,
            state,
            () => Task.FromResult<ChatCompletion?>(null));

        // Assert
        Assert.False(result.WasLazy);
        Assert.Equal(completion, result.FinalCompletion);
        Assert.Null(result.LazyResponse);
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task HandleLazyModeAsync_WhenLazy_ShouldLogActivation()
    {
        // Arrange
        var lazyFunc = CreateMockFunction("lazyFunc", canBeLazy: true);
        _functions["lazyFunc"] = lazyFunc;

        var handler = CreateHandler();
        var state = CreateState(Kotonoha.Akane);
        state.PatienceCount = 4;

        var toolCall = CreateToolCall("lazyFunc");
        var completion = CreateChatCompletion(ChatFinishReason.ToolCalls, null, [toolCall]);

        var refusalCompletion = CreateChatCompletion(
            ChatFinishReason.Stop,
            "{\"Assistant\": \"Akane\", \"Text\": \"葵、任せたで\", \"Emotion\": \"Calm\"}");
        var acceptanceCompletion = CreateChatCompletion(
            ChatFinishReason.ToolCalls,
            null,
            [toolCall]);

        var callCount = 0;

        // Act
        await handler.HandleLazyModeAsync(
            completion,
            state,
            () => Task.FromResult<ChatCompletion?>(
                ++callCount == 1 ? refusalCompletion : acceptanceCompletion));

        // Assert
        _mockLogger.Verify(
            x => x.LogInformation(It.Is<string>(s => s.Contains("[LazyMode]") && s.Contains("activated"))),
            Times.Once);
    }

    [Fact]
    public async Task HandleLazyModeAsync_WhenCancelled_ShouldLogWarning()
    {
        // Arrange
        var lazyFunc = CreateMockFunction("lazyFunc", canBeLazy: true);
        _functions["lazyFunc"] = lazyFunc;

        var handler = CreateHandler();
        var state = CreateState(Kotonoha.Akane);
        state.PatienceCount = 4;

        var toolCall = CreateToolCall("lazyFunc");
        var completion = CreateChatCompletion(ChatFinishReason.ToolCalls, null, [toolCall]);

        // 拒否応答でもツール呼び出しがある（キャンセルケース）
        var refusalWithToolCalls = CreateChatCompletion(
            ChatFinishReason.ToolCalls,
            null,
            [toolCall]);

        // Act
        await handler.HandleLazyModeAsync(
            completion,
            state,
            () => Task.FromResult<ChatCompletion?>(refusalWithToolCalls));

        // Assert
        _mockLogger.Verify(
            x => x.LogWarning(It.Is<string>(s => s.Contains("[LazyMode]") && s.Contains("cancelled"))),
            Times.Once);
    }

    [Fact]
    public async Task HandleLazyModeAsync_WhenCompleted_ShouldLogSuccess()
    {
        // Arrange
        var lazyFunc = CreateMockFunction("lazyFunc", canBeLazy: true);
        _functions["lazyFunc"] = lazyFunc;

        var handler = CreateHandler();
        var state = CreateState(Kotonoha.Akane);
        state.PatienceCount = 4;

        var toolCall = CreateToolCall("lazyFunc");
        var completion = CreateChatCompletion(ChatFinishReason.ToolCalls, null, [toolCall]);

        var refusalCompletion = CreateChatCompletion(
            ChatFinishReason.Stop,
            "{\"Assistant\": \"Akane\", \"Text\": \"葵、任せたで\", \"Emotion\": \"Calm\"}");
        var acceptanceCompletion = CreateChatCompletion(
            ChatFinishReason.ToolCalls,
            null,
            [toolCall]);

        var callCount = 0;

        // Act
        await handler.HandleLazyModeAsync(
            completion,
            state,
            () => Task.FromResult<ChatCompletion?>(
                ++callCount == 1 ? refusalCompletion : acceptanceCompletion));

        // Assert
        _mockLogger.Verify(
            x => x.LogInformation(It.Is<string>(s => s.Contains("[LazyMode]") && s.Contains("completed successfully"))),
            Times.Once);
    }

    #endregion
}
