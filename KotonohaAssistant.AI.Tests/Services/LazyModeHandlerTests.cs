using FluentAssertions;
using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Utils;
using OpenAI.Chat;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KotonohaAssistant.AI.Tests.Services;

public class LazyModeHandlerTests
{
    #region Test Helpers

    /// <summary>
    /// テスト用のモック IRandomGenerator
    /// </summary>
    private class MockRandomGenerator : IRandomGenerator
    {
        private readonly double _value;

        public MockRandomGenerator(double value)
        {
            _value = value;
        }

        public double NextDouble() => _value;
    }

    /// <summary>
    /// テスト用のモック ToolFunction
    /// </summary>
    private class MockToolFunction : ToolFunction
    {
        private readonly bool _canBeLazy;

        public MockToolFunction(bool canBeLazy, ILogger logger) : base(logger)
        {
            _canBeLazy = canBeLazy;
        }

        public override bool CanBeLazy => _canBeLazy;
        public override string Description => "Mock function";
        public override string Parameters => "{}";

        public override bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments)
        {
            arguments = new Dictionary<string, object>();
            return true;
        }

        public override Task<string> Invoke(IDictionary<string, object> arguments, ConversationState state)
        {
            return Task.FromResult("{}");
        }
    }

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
    /// テスト用の ConversationState を作成
    /// </summary>
    private ConversationState CreateTestState(
        Kotonoha currentSister = Kotonoha.Akane,
        int patienceCount = 0)
    {
        return new ConversationState
        {
            SystemMessageAkane = "System message for Akane",
            SystemMessageAoi = "System message for Aoi",
            CurrentSister = currentSister,
            PatienceCount = patienceCount,
            ChatMessages = ImmutableArray<ChatMessage>.Empty
        };
    }

    /// <summary>
    /// テスト用の関数辞書を作成
    /// </summary>
    private Dictionary<string, ToolFunction> CreateFunctionDictionary(bool canBeLazy = true)
    {
        var logger = new MockLogger();
        return new Dictionary<string, ToolFunction>
        {
            ["test_function"] = new MockToolFunction(canBeLazy, logger)
        };
    }

    /// <summary>
    /// テスト用の ChatCompletion を作成（Stop のみ）
    /// </summary>
    private ChatCompletion CreateStopCompletion()
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
    /// テスト用の ChatCompletion を作成（ToolCalls）
    /// </summary>
    private ChatCompletion CreateToolCallsCompletion(string functionName = "test_function")
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
    private ChatCompletion CreateMultipleToolCallsCompletion(params string[] functionNames)
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

    /// <summary>
    /// テスト用の ChatCompletion を作成（テキスト応答）
    /// </summary>
    private ChatCompletion CreateTextCompletion(string text)
    {
        return OpenAIChatModelFactory.ChatCompletion(
            id: "test-id",
            model: "gpt-4",
            createdAt: DateTimeOffset.FromUnixTimeSeconds(1234567890),
            finishReason: ChatFinishReason.Stop,
            role: ChatMessageRole.Assistant,
            content: [ChatMessageContentPart.CreateTextPart(text)]
        );
    }

    #endregion

    #region ShouldBeLazy の挙動テスト（HandleLazyModeAsync経由）

    [Fact]
    public async Task HandleLazyModeAsync_FinishReasonがToolCallsでない場合_怠けないこと()
    {
        // Arrange
        var functions = CreateFunctionDictionary();
        var logger = new MockLogger();
        var randomGenerator = new MockRandomGenerator(0.05); // 怠ける確率
        var handler = new LazyModeHandler(functions, logger, randomGenerator);

        var state = CreateTestState();
        var completion = CreateStopCompletion(); // FinishReason = Stop
        var dateTime = new DateTime(2025, 1, 1);

        var regenerateCalled = false;
        Task<ChatCompletion?> RegenerateAsync(ConversationState s)
        {
            regenerateCalled = true;
            return Task.FromResult<ChatCompletion?>(null);
        }

        // Act
        var (result, newState) = await handler.HandleLazyModeAsync(
            completion, state, dateTime, RegenerateAsync);

        // Assert
        result.WasLazy.Should().BeFalse();
        result.LazyResponse.Should().BeNull();
        result.FinalCompletion.Should().BeSameAs(completion);
        newState.ChatMessages.Should().BeEmpty();
        regenerateCalled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleLazyModeAsync_CanBeLazyがfalseの関数を含む場合_怠けないこと()
    {
        // Arrange
        var functions = CreateFunctionDictionary(canBeLazy: false); // CanBeLazy = false
        var logger = new MockLogger();
        var randomGenerator = new MockRandomGenerator(0.05); // 怠ける確率
        var handler = new LazyModeHandler(functions, logger, randomGenerator);

        var state = CreateTestState();
        var completion = CreateToolCallsCompletion("test_function"); // FinishReason = ToolCalls
        var dateTime = new DateTime(2025, 1, 1);

        var regenerateCalled = false;
        Task<ChatCompletion?> RegenerateAsync(ConversationState s)
        {
            regenerateCalled = true;
            return Task.FromResult<ChatCompletion?>(null);
        }

        // Act
        var (result, newState) = await handler.HandleLazyModeAsync(
            completion, state, dateTime, RegenerateAsync);

        // Assert
        result.WasLazy.Should().BeFalse();
        result.LazyResponse.Should().BeNull();
        result.FinalCompletion.Should().BeSameAs(completion);
        newState.ChatMessages.Should().BeEmpty();
        regenerateCalled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleLazyModeAsync_PatienceCountが3より大きい場合_必ず怠けること()
    {
        // Arrange
        var functions = CreateFunctionDictionary(canBeLazy: true);
        var logger = new MockLogger();
        var randomGenerator = new MockRandomGenerator(0.5); // 怠けない確率だが、PatienceCount > 3 なので無視される
        var handler = new LazyModeHandler(functions, logger, randomGenerator);

        var state = CreateTestState(patienceCount: 4); // PatienceCount > 3
        var completion = CreateToolCallsCompletion("test_function");
        var dateTime = new DateTime(2025, 1, 1);

        var regenerateCallCount = 0;
        var lazyResponseJson = """{"Assistant":"Akane","Text":"葵、任せたで"}""";
        var acceptResponseJson = """{"Assistant":"Aoi","Text":"わかりました"}""";

        Task<ChatCompletion?> RegenerateAsync(ConversationState s)
        {
            regenerateCallCount++;
            if (regenerateCallCount == 1)
            {
                // 1回目: 怠け癖応答
                return Task.FromResult<ChatCompletion?>(CreateTextCompletion(lazyResponseJson));
            }
            else
            {
                // 2回目: 引き受ける応答
                return Task.FromResult<ChatCompletion?>(CreateTextCompletion(acceptResponseJson));
            }
        }

        // Act
        var (result, newState) = await handler.HandleLazyModeAsync(
            completion, state, dateTime, RegenerateAsync);

        // Assert
        result.WasLazy.Should().BeTrue();
        result.LazyResponse.Should().NotBeNull();
        result.LazyResponse!.Message.Should().Be("葵、任せたで");
        result.LazyResponse.Sister.Should().Be(Kotonoha.Akane);
        result.FinalCompletion.Should().NotBeSameAs(completion);
        regenerateCallCount.Should().Be(2);
    }

    [Fact]
    public async Task HandleLazyModeAsync_PatienceCount3以下でランダムがfalse_怠けないこと()
    {
        // Arrange
        var functions = CreateFunctionDictionary(canBeLazy: true);
        var logger = new MockLogger();
        var randomGenerator = new MockRandomGenerator(0.1); // 0.1 以上なので怠けない
        var handler = new LazyModeHandler(functions, logger, randomGenerator);

        var state = CreateTestState(patienceCount: 3); // PatienceCount <= 3
        var completion = CreateToolCallsCompletion("test_function");
        var dateTime = new DateTime(2025, 1, 1);

        var regenerateCalled = false;
        Task<ChatCompletion?> RegenerateAsync(ConversationState s)
        {
            regenerateCalled = true;
            return Task.FromResult<ChatCompletion?>(null);
        }

        // Act
        var (result, newState) = await handler.HandleLazyModeAsync(
            completion, state, dateTime, RegenerateAsync);

        // Assert
        result.WasLazy.Should().BeFalse();
        result.LazyResponse.Should().BeNull();
        result.FinalCompletion.Should().BeSameAs(completion);
        newState.ChatMessages.Should().BeEmpty();
        regenerateCalled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleLazyModeAsync_PatienceCount3以下でランダムがtrue_怠けること()
    {
        // Arrange
        var functions = CreateFunctionDictionary(canBeLazy: true);
        var logger = new MockLogger();
        var randomGenerator = new MockRandomGenerator(0.05); // 0.1 未満なので怠ける
        var handler = new LazyModeHandler(functions, logger, randomGenerator);

        var state = CreateTestState(patienceCount: 2); // PatienceCount <= 3
        var completion = CreateToolCallsCompletion("test_function");
        var dateTime = new DateTime(2025, 1, 1);

        var regenerateCallCount = 0;
        var lazyResponseJson = """{"Assistant":"Akane","Text":"葵、任せたで"}""";
        var acceptResponseJson = """{"Assistant":"Aoi","Text":"わかりました"}""";

        Task<ChatCompletion?> RegenerateAsync(ConversationState s)
        {
            regenerateCallCount++;
            if (regenerateCallCount == 1)
            {
                // 1回目: 怠け癖応答
                return Task.FromResult<ChatCompletion?>(CreateTextCompletion(lazyResponseJson));
            }
            else
            {
                // 2回目: 引き受ける応答
                return Task.FromResult<ChatCompletion?>(CreateTextCompletion(acceptResponseJson));
            }
        }

        // Act
        var (result, newState) = await handler.HandleLazyModeAsync(
            completion, state, dateTime, RegenerateAsync);

        // Assert
        result.WasLazy.Should().BeTrue();
        result.LazyResponse.Should().NotBeNull();
        result.LazyResponse!.Message.Should().Be("葵、任せたで");
        result.FinalCompletion.Should().NotBeSameAs(completion);
        regenerateCallCount.Should().Be(2);
    }

    #endregion

    #region 怠け癖発動時の正常フローテスト

    [Fact]
    public async Task HandleLazyModeAsync_怠け癖発動_正常に完了すること()
    {
        // Arrange
        var functions = CreateFunctionDictionary(canBeLazy: true);
        var logger = new MockLogger();
        var randomGenerator = new MockRandomGenerator(0.05); // 怠ける確率
        var handler = new LazyModeHandler(functions, logger, randomGenerator);

        var state = CreateTestState(currentSister: Kotonoha.Akane, patienceCount: 0);
        var completion = CreateToolCallsCompletion("test_function");
        var dateTime = new DateTime(2025, 1, 1);

        var regenerateCallCount = 0;
        var capturedStates = new List<ConversationState>();
        var lazyResponseJson = """{"Assistant":"Akane","Text":"葵、任せたで"}""";
        var acceptResponseJson = """{"Assistant":"Aoi","Text":"わかりました"}""";

        Task<ChatCompletion?> RegenerateAsync(ConversationState s)
        {
            capturedStates.Add(s);
            regenerateCallCount++;
            if (regenerateCallCount == 1)
            {
                // 1回目: 怠け癖応答
                return Task.FromResult<ChatCompletion?>(CreateTextCompletion(lazyResponseJson));
            }
            else
            {
                // 2回目: 引き受ける応答
                return Task.FromResult<ChatCompletion?>(CreateTextCompletion(acceptResponseJson));
            }
        }

        // Act
        var (result, newState) = await handler.HandleLazyModeAsync(
            completion, state, dateTime, RegenerateAsync);

        // Assert
        // 1. regenerateCompletionAsync が2回呼ばれること
        regenerateCallCount.Should().Be(2);

        // 2. 1回目の呼び出し時: BeginLazyMode instruction が追加されている
        var firstCallState = capturedStates[0];
        firstCallState.ChatMessages.Should().HaveCount(1);
        firstCallState.CurrentSister.Should().Be(Kotonoha.Akane);

        // 3. 2回目の呼び出し時: 姉妹が切り替わり、怠け癖応答とEndLazyMode instructionが追加されている
        var secondCallState = capturedStates[1];
        secondCallState.CurrentSister.Should().Be(Kotonoha.Aoi);
        secondCallState.ChatMessages.Should().HaveCount(3); // BeginLazy + 怠け癖応答 + EndLazy

        // 4. LazyResponse が設定される
        result.LazyResponse.Should().NotBeNull();
        result.LazyResponse!.Message.Should().Be("葵、任せたで");
        result.LazyResponse.Sister.Should().Be(Kotonoha.Akane);
        result.LazyResponse.Functions.Should().BeEmpty();

        // 5. WasLazy = true
        result.WasLazy.Should().BeTrue();

        // 6. FinalCompletion = 引き受ける応答
        result.FinalCompletion.Should().NotBeSameAs(completion);
        result.FinalCompletion.Content[0].Text.Should().Be(acceptResponseJson);
    }

    [Fact]
    public async Task HandleLazyModeAsync_茜から葵への怠け癖委譲_正しいインストラクションが追加されること()
    {
        // Arrange
        var functions = CreateFunctionDictionary(canBeLazy: true);
        var logger = new MockLogger();
        var randomGenerator = new MockRandomGenerator(0.05); // 怠ける確率
        var handler = new LazyModeHandler(functions, logger, randomGenerator);

        var state = CreateTestState(currentSister: Kotonoha.Akane, patienceCount: 0);
        var completion = CreateToolCallsCompletion("test_function");
        var dateTime = new DateTime(2025, 1, 1);

        var capturedStates = new List<ConversationState>();
        var lazyResponseJson = """{"Assistant":"Akane","Text":"葵、任せたで"}""";
        var acceptResponseJson = """{"Assistant":"Aoi","Text":"わかりました"}""";

        Task<ChatCompletion?> RegenerateAsync(ConversationState s)
        {
            capturedStates.Add(s);
            if (capturedStates.Count == 1)
            {
                return Task.FromResult<ChatCompletion?>(CreateTextCompletion(lazyResponseJson));
            }
            else
            {
                return Task.FromResult<ChatCompletion?>(CreateTextCompletion(acceptResponseJson));
            }
        }

        // Act
        var (result, newState) = await handler.HandleLazyModeAsync(
            completion, state, dateTime, RegenerateAsync);

        // Assert
        // 1. CurrentSister = Akane の状態で怠け癖発動
        state.CurrentSister.Should().Be(Kotonoha.Akane);

        // 2. BeginLazyModeAkane instructionが挿入される
        var firstCallState = capturedStates[0];
        var beginLazyMessage = firstCallState.ChatMessages[0];
        var beginLazyJson = JsonNode.Parse(beginLazyMessage.Content[0].Text);
        beginLazyJson.Should().NotBeNull();
        beginLazyJson.Should().HavePropertyWithStringValue("InputType", "Instruction");
        beginLazyJson.Should().HavePropertyWithStringValue("Text", Instruction.BeginLazyModeAkane);

        // 3. 姉妹が Aoi に切り替わる
        var secondCallState = capturedStates[1];
        secondCallState.CurrentSister.Should().Be(Kotonoha.Aoi);

        // 4. EndLazyModeAoi instructionが挿入される
        var endLazyMessage = secondCallState.ChatMessages[2];
        var endLazyJson = JsonNode.Parse(endLazyMessage.Content[0].Text);
        endLazyJson.Should().NotBeNull();
        endLazyJson.Should().HavePropertyWithStringValue("InputType", "Instruction");
        endLazyJson.Should().HavePropertyWithStringValue("Text", Instruction.EndLazyModeAoi);

        // 5. 怠け癖が正常に完了
        result.WasLazy.Should().BeTrue();
    }

    [Fact]
    public async Task HandleLazyModeAsync_葵から茜への怠け癖委譲_正しいインストラクションが追加されること()
    {
        // Arrange
        var functions = CreateFunctionDictionary(canBeLazy: true);
        var logger = new MockLogger();
        var randomGenerator = new MockRandomGenerator(0.05); // 怠ける確率
        var handler = new LazyModeHandler(functions, logger, randomGenerator);

        var state = CreateTestState(currentSister: Kotonoha.Aoi, patienceCount: 0);
        var completion = CreateToolCallsCompletion("test_function");
        var dateTime = new DateTime(2025, 1, 1);

        var capturedStates = new List<ConversationState>();
        var lazyResponseJson = """{"Assistant":"Aoi","Text":"お姉ちゃんお願い"}""";
        var acceptResponseJson = """{"Assistant":"Akane","Text":"しゃあないなあ"}""";

        Task<ChatCompletion?> RegenerateAsync(ConversationState s)
        {
            capturedStates.Add(s);
            if (capturedStates.Count == 1)
            {
                return Task.FromResult<ChatCompletion?>(CreateTextCompletion(lazyResponseJson));
            }
            else
            {
                return Task.FromResult<ChatCompletion?>(CreateTextCompletion(acceptResponseJson));
            }
        }

        // Act
        var (result, newState) = await handler.HandleLazyModeAsync(
            completion, state, dateTime, RegenerateAsync);

        // Assert
        // 1. CurrentSister = Aoi の状態で怠け癖発動
        state.CurrentSister.Should().Be(Kotonoha.Aoi);

        // 2. BeginLazyModeAoi instructionが挿入される
        var firstCallState = capturedStates[0];
        var beginLazyMessage = firstCallState.ChatMessages[0];
        var beginLazyJson = JsonNode.Parse(beginLazyMessage.Content[0].Text);
        beginLazyJson.Should().NotBeNull();
        beginLazyJson.Should().HavePropertyWithStringValue("InputType", "Instruction");
        beginLazyJson.Should().HavePropertyWithStringValue("Text", Instruction.BeginLazyModeAoi);

        // 3. 姉妹が Akane に切り替わる
        var secondCallState = capturedStates[1];
        secondCallState.CurrentSister.Should().Be(Kotonoha.Akane);

        // 4. EndLazyModeAkane instructionが挿入される
        var endLazyMessage = secondCallState.ChatMessages[2];
        var endLazyJson = JsonNode.Parse(endLazyMessage.Content[0].Text);
        endLazyJson.Should().NotBeNull();
        endLazyJson.Should().HavePropertyWithStringValue("InputType", "Instruction");
        endLazyJson.Should().HavePropertyWithStringValue("Text", Instruction.EndLazyModeAkane);

        // 5. 怠け癖が正常に完了
        result.WasLazy.Should().BeTrue();
    }

    [Fact]
    public async Task HandleLazyModeAsync_LazyResponseが正しくパースされること()
    {
        // Arrange
        var functions = CreateFunctionDictionary(canBeLazy: true);
        var logger = new MockLogger();
        var randomGenerator = new MockRandomGenerator(0.05); // 怠ける確率
        var handler = new LazyModeHandler(functions, logger, randomGenerator);

        var state = CreateTestState(currentSister: Kotonoha.Akane, patienceCount: 0);
        var completion = CreateToolCallsCompletion("test_function");
        var dateTime = new DateTime(2025, 1, 1);

        var regenerateCallCount = 0;
        var expectedMessage = "葵、任せたで";
        var expectedSister = Kotonoha.Akane;
        var lazyResponseJson = $$"""{"Assistant":"{{expectedSister}}","Text":"{{expectedMessage}}"}""";
        var acceptResponseJson = """{"Assistant":"Aoi","Text":"わかりました"}""";

        Task<ChatCompletion?> RegenerateAsync(ConversationState s)
        {
            regenerateCallCount++;
            if (regenerateCallCount == 1)
            {
                return Task.FromResult<ChatCompletion?>(CreateTextCompletion(lazyResponseJson));
            }
            else
            {
                return Task.FromResult<ChatCompletion?>(CreateTextCompletion(acceptResponseJson));
            }
        }

        // Act
        var (result, newState) = await handler.HandleLazyModeAsync(
            completion, state, dateTime, RegenerateAsync);

        // Assert
        // 1. LazyResponse が正しくパースされる
        result.LazyResponse.Should().NotBeNull();

        // 2. LazyResponse.Message に応答テキストが設定される
        result.LazyResponse!.Message.Should().Be(expectedMessage);

        // 3. LazyResponse.Sister に正しい姉妹名が設定される
        result.LazyResponse.Sister.Should().Be(expectedSister);

        // 4. LazyResponse.Functions は空配列
        result.LazyResponse.Functions.Should().NotBeNull();
        result.LazyResponse.Functions.Should().BeEmpty();
    }

    #endregion

    #region 怠け癖キャンセルテスト

    [Fact]
    public async Task HandleLazyModeAsync_怠け癖応答でToolCallsが返された場合_キャンセルすること()
    {
        // 期待される挙動:
        // - BeginLazyMode instruction 追加後、regenerateCompletionAsync を呼ぶ
        // - regenerateCompletionAsync が FinishReason = ToolCalls を返す
        // - CancelLazyMode instruction が state に追加される
        // - WasLazy = false
        // - LazyResponse = null
        // - FinalCompletion = 元の completion

        // Arrange
        var functions = CreateFunctionDictionary(canBeLazy: true);
        var logger = new MockLogger();
        var randomGenerator = new MockRandomGenerator(0.05); // 怠ける確率
        var handler = new LazyModeHandler(functions, logger, randomGenerator);

        var state = CreateTestState(currentSister: Kotonoha.Akane, patienceCount: 0);
        var completion = CreateToolCallsCompletion("test_function");
        var dateTime = new DateTime(2025, 1, 1);

        var regenerateCalled = false;
        Task<ChatCompletion?> RegenerateAsync(ConversationState s)
        {
            regenerateCalled = true;
            // 怠け癖応答として ToolCalls を返す（異常系）
            return Task.FromResult<ChatCompletion?>(CreateToolCallsCompletion("another_function"));
        }

        // Act
        var (result, newState) = await handler.HandleLazyModeAsync(
            completion, state, dateTime, RegenerateAsync);

        // Assert
        result.WasLazy.Should().BeFalse();
        result.LazyResponse.Should().BeNull();
        result.FinalCompletion.Should().BeSameAs(completion);
        regenerateCalled.Should().BeTrue();

        // CancelLazyMode instruction が追加されている
        newState.ChatMessages.Should().HaveCount(2);
        var cancelMessage = newState.ChatMessages[1];
        var cancelJson = JsonNode.Parse(cancelMessage.Content[0].Text);
        cancelJson.Should().NotBeNull();
        cancelJson.Should().HavePropertyWithStringValue("InputType", "Instruction");
        cancelJson.Should().HavePropertyWithStringValue("Text", Instruction.CancelLazyMode);
    }

    [Fact]
    public async Task HandleLazyModeAsync_怠け癖応答がnullの場合_キャンセルすること()
    {
        // 期待される挙動:
        // - BeginLazyMode instruction 追加後、regenerateCompletionAsync を呼ぶ
        // - regenerateCompletionAsync が null を返す
        // - CancelLazyMode instruction が state に追加される
        // - WasLazy = false
        // - LazyResponse = null
        // - FinalCompletion = 元の completion

        // Arrange
        var functions = CreateFunctionDictionary(canBeLazy: true);
        var logger = new MockLogger();
        var randomGenerator = new MockRandomGenerator(0.05); // 怠ける確率
        var handler = new LazyModeHandler(functions, logger, randomGenerator);

        var state = CreateTestState(currentSister: Kotonoha.Aoi, patienceCount: 0);
        var completion = CreateToolCallsCompletion("test_function");
        var dateTime = new DateTime(2025, 1, 1);

        var regenerateCalled = false;
        Task<ChatCompletion?> RegenerateAsync(ConversationState s)
        {
            regenerateCalled = true;
            // 怠け癖応答として null を返す（異常系）
            return Task.FromResult<ChatCompletion?>(null);
        }

        // Act
        var (result, newState) = await handler.HandleLazyModeAsync(
            completion, state, dateTime, RegenerateAsync);

        // Assert
        result.WasLazy.Should().BeFalse();
        result.LazyResponse.Should().BeNull();
        result.FinalCompletion.Should().BeSameAs(completion);
        regenerateCalled.Should().BeTrue();

        // CancelLazyMode instruction が追加されている
        newState.ChatMessages.Should().HaveCount(2);
        var cancelMessage = newState.ChatMessages[1];
        var cancelJson = JsonNode.Parse(cancelMessage.Content[0].Text);
        cancelJson.Should().NotBeNull();
        cancelJson.Should().HavePropertyWithStringValue("InputType", "Instruction");
        cancelJson.Should().HavePropertyWithStringValue("Text", Instruction.CancelLazyMode);
    }

    #endregion

    #region 引き受ける応答の生成失敗テスト

    [Fact]
    public async Task HandleLazyModeAsync_引き受ける応答がnullの場合_元のcompletionを返すこと()
    {
        // 期待される挙動:
        // - 怠け癖応答は正常に生成される
        // - 姉妹切り替え後、regenerateCompletionAsync（2回目）が null を返す
        // - WasLazy = false
        // - LazyResponse = null
        // - FinalCompletion = 元の completion
        // - state には怠け癖応答までのメッセージが含まれている

        // Arrange
        var functions = CreateFunctionDictionary(canBeLazy: true);
        var logger = new MockLogger();
        var randomGenerator = new MockRandomGenerator(0.05); // 怠ける確率
        var handler = new LazyModeHandler(functions, logger, randomGenerator);

        var state = CreateTestState(currentSister: Kotonoha.Akane, patienceCount: 0);
        var completion = CreateToolCallsCompletion("test_function");
        var dateTime = new DateTime(2025, 1, 1);

        var regenerateCallCount = 0;
        var lazyResponseJson = """{"Assistant":"Akane","Text":"葵、任せたで"}""";

        Task<ChatCompletion?> RegenerateAsync(ConversationState s)
        {
            regenerateCallCount++;
            if (regenerateCallCount == 1)
            {
                // 1回目: 怠け癖応答は正常に生成
                return Task.FromResult<ChatCompletion?>(CreateTextCompletion(lazyResponseJson));
            }
            else
            {
                // 2回目: 引き受ける応答が null を返す（異常系）
                return Task.FromResult<ChatCompletion?>(null);
            }
        }

        // Act
        var (result, newState) = await handler.HandleLazyModeAsync(
            completion, state, dateTime, RegenerateAsync);

        // Assert
        result.WasLazy.Should().BeFalse();
        result.LazyResponse.Should().BeNull();
        result.FinalCompletion.Should().BeSameAs(completion);
        regenerateCallCount.Should().Be(2);

        // state には怠け癖応答までのメッセージが含まれている
        // BeginLazy + 怠け癖応答 + EndLazy の3つ
        newState.ChatMessages.Should().HaveCount(3);
    }

    #endregion

    #region regenerateCompletionAsync 呼び出しテスト

    [Fact]
    public async Task HandleLazyModeAsync_怠け癖発動時_regenerateCompletionAsyncが正しいstateで呼ばれること()
    {
        // 期待される挙動:
        // - 1回目の regenerateCompletionAsync 呼び出し時:
        //   - state に BeginLazyMode instruction が追加されている
        //   - CurrentSister は元のまま
        // - 2回目の regenerateCompletionAsync 呼び出し時:
        //   - state に怠け癖応答が追加されている
        //   - CurrentSister が切り替わっている
        //   - state に EndLazyMode instruction が追加されている

        // Arrange
        var functions = CreateFunctionDictionary(canBeLazy: true);
        var logger = new MockLogger();
        var randomGenerator = new MockRandomGenerator(0.05); // 怠ける確率
        var handler = new LazyModeHandler(functions, logger, randomGenerator);

        var state = CreateTestState(currentSister: Kotonoha.Akane, patienceCount: 0);
        var completion = CreateToolCallsCompletion("test_function");
        var dateTime = new DateTime(2025, 1, 1);

        var capturedStates = new List<ConversationState>();
        var lazyResponseJson = """{"Assistant":"Akane","Text":"葵、任せたで"}""";
        var acceptResponseJson = """{"Assistant":"Aoi","Text":"わかりました"}""";

        Task<ChatCompletion?> RegenerateAsync(ConversationState s)
        {
            capturedStates.Add(s);
            if (capturedStates.Count == 1)
            {
                return Task.FromResult<ChatCompletion?>(CreateTextCompletion(lazyResponseJson));
            }
            else
            {
                return Task.FromResult<ChatCompletion?>(CreateTextCompletion(acceptResponseJson));
            }
        }

        // Act
        var (result, newState) = await handler.HandleLazyModeAsync(
            completion, state, dateTime, RegenerateAsync);

        // Assert
        capturedStates.Should().HaveCount(2);

        // 1回目の呼び出し時
        var firstCallState = capturedStates[0];
        firstCallState.CurrentSister.Should().Be(Kotonoha.Akane);
        firstCallState.ChatMessages.Should().HaveCount(1);
        var beginLazyJson = JsonNode.Parse(firstCallState.ChatMessages[0].Content[0].Text);
        beginLazyJson.Should().NotBeNull();
        beginLazyJson.Should().HavePropertyWithStringValue("InputType", "Instruction");
        beginLazyJson.Should().HavePropertyWithStringValue("Text", Instruction.BeginLazyModeAkane);

        // 2回目の呼び出し時
        var secondCallState = capturedStates[1];
        secondCallState.CurrentSister.Should().Be(Kotonoha.Aoi);
        secondCallState.ChatMessages.Should().HaveCount(3);

        // BeginLazy instruction
        var beginLazyMessage = secondCallState.ChatMessages[0];
        var beginLazyMessageJson = JsonNode.Parse(beginLazyMessage.Content[0].Text);
        beginLazyMessageJson.Should().NotBeNull();
        beginLazyMessageJson.Should().HavePropertyWithStringValue("Text", Instruction.BeginLazyModeAkane);

        // 怠け癖応答
        var lazyResponseMessage = secondCallState.ChatMessages[1];
        lazyResponseMessage.Content[0].Text.Should().Be(lazyResponseJson);

        // EndLazy instruction
        var endLazyMessage = secondCallState.ChatMessages[2];
        var endLazyJson = JsonNode.Parse(endLazyMessage.Content[0].Text);
        endLazyJson.Should().NotBeNull();
        endLazyJson.Should().HavePropertyWithStringValue("InputType", "Instruction");
        endLazyJson.Should().HavePropertyWithStringValue("Text", Instruction.EndLazyModeAoi);
    }

    #endregion

    #region エッジケーステスト

    [Fact]
    public async Task HandleLazyModeAsync_関数辞書に存在しない関数が呼ばれた場合_スキップすること()
    {
        // 期待される挙動:
        // - completion.ToolCalls に _functions に存在しない関数名が含まれる
        // - その関数は ShouldBeLazy の判定から除外される
        // - 他の条件次第で怠け癖は発動しうる

        // Arrange
        var functions = CreateFunctionDictionary(canBeLazy: true);
        var logger = new MockLogger();
        var randomGenerator = new MockRandomGenerator(0.5); // 怠けない確率
        var handler = new LazyModeHandler(functions, logger, randomGenerator);

        var state = CreateTestState(currentSister: Kotonoha.Akane, patienceCount: 0);
        // 存在しない関数名を含む completion
        var completion = CreateToolCallsCompletion("non_existent_function");
        var dateTime = new DateTime(2025, 1, 1);

        var regenerateCalled = false;
        Task<ChatCompletion?> RegenerateAsync(ConversationState s)
        {
            regenerateCalled = true;
            return Task.FromResult<ChatCompletion?>(null);
        }

        // Act
        var (result, newState) = await handler.HandleLazyModeAsync(
            completion, state, dateTime, RegenerateAsync);

        // Assert
        // 辞書に存在しない関数は判定から除外されるため、怠けない
        result.WasLazy.Should().BeFalse();
        result.LazyResponse.Should().BeNull();
        result.FinalCompletion.Should().BeSameAs(completion);
        regenerateCalled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleLazyModeAsync_複数関数呼び出しで一部がCanBeLazy_false_怠けないこと()
    {
        // 期待される挙動:
        // - completion.ToolCalls に複数の関数が含まれる
        // - そのうち1つでも CanBeLazy = false なら怠けない
        // - WasLazy = false
        // - FinalCompletion = 元の completion

        // Arrange
        var logger = new MockLogger();
        var functions = new Dictionary<string, ToolFunction>
        {
            ["lazy_function"] = new MockToolFunction(canBeLazy: true, logger),
            ["non_lazy_function"] = new MockToolFunction(canBeLazy: false, logger)
        };
        var randomGenerator = new MockRandomGenerator(0.05); // 怠ける確率
        var handler = new LazyModeHandler(functions, logger, randomGenerator);

        var state = CreateTestState(currentSister: Kotonoha.Akane, patienceCount: 0);
        // 複数の関数呼び出しを含む completion（一部が CanBeLazy = false）
        var completion = CreateMultipleToolCallsCompletion("lazy_function", "non_lazy_function");
        var dateTime = new DateTime(2025, 1, 1);

        var regenerateCalled = false;
        Task<ChatCompletion?> RegenerateAsync(ConversationState s)
        {
            regenerateCalled = true;
            return Task.FromResult<ChatCompletion?>(null);
        }

        // Act
        var (result, newState) = await handler.HandleLazyModeAsync(
            completion, state, dateTime, RegenerateAsync);

        // Assert
        // 1つでも CanBeLazy = false の関数があれば怠けない
        result.WasLazy.Should().BeFalse();
        result.LazyResponse.Should().BeNull();
        result.FinalCompletion.Should().BeSameAs(completion);
        regenerateCalled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleLazyModeAsync_怠け癖応答のJSONパースに失敗した場合_LazyResponseはnullでないこと()
    {
        // 期待される挙動:
        // - 怠け癖応答の Content[0].Text が不正な JSON
        // - ChatResponse.TryParse が false を返す
        // - LazyResponse は設定されない（response が null なので）
        // - ただし WasLazy = true
        // - FinalCompletion = 引き受ける応答

        // Arrange
        var functions = CreateFunctionDictionary(canBeLazy: true);
        var logger = new MockLogger();
        var randomGenerator = new MockRandomGenerator(0.05); // 怠ける確率
        var handler = new LazyModeHandler(functions, logger, randomGenerator);

        var state = CreateTestState(currentSister: Kotonoha.Akane, patienceCount: 0);
        var completion = CreateToolCallsCompletion("test_function");
        var dateTime = new DateTime(2025, 1, 1);

        var regenerateCallCount = 0;
        var invalidJson = "This is not valid JSON"; // 不正な JSON
        var acceptResponseJson = """{"Assistant":"Aoi","Text":"わかりました"}""";

        Task<ChatCompletion?> RegenerateAsync(ConversationState s)
        {
            regenerateCallCount++;
            if (regenerateCallCount == 1)
            {
                // 1回目: 不正な JSON を返す
                return Task.FromResult<ChatCompletion?>(CreateTextCompletion(invalidJson));
            }
            else
            {
                // 2回目: 引き受ける応答
                return Task.FromResult<ChatCompletion?>(CreateTextCompletion(acceptResponseJson));
            }
        }

        // Act
        var (result, newState) = await handler.HandleLazyModeAsync(
            completion, state, dateTime, RegenerateAsync);

        // Assert
        // JSON パースに失敗するが、怠け癖フローは完了する
        result.WasLazy.Should().BeTrue();
        result.LazyResponse.Should().BeNull(); // パース失敗により null
        result.FinalCompletion.Should().NotBeSameAs(completion);
        result.FinalCompletion.Content[0].Text.Should().Be(acceptResponseJson);
        regenerateCallCount.Should().Be(2);
    }

    #endregion
}
