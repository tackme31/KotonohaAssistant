using System.ClientModel.Primitives;
using System.Text.Json;
using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Utils;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Tests.Helpers;

/// <summary>
/// テスト用のモック ILogger（ログ記録機能付き）
/// </summary>
public class MockLogger : ILogger
{
    public List<string> InformationLogs { get; } = [];
    public List<string> WarningLogs { get; } = [];
    public List<string> ErrorLogs { get; } = [];
    public List<Exception> Errors { get; } = [];

    public void LogInformation(string message)
    {
        InformationLogs.Add(message);
    }

    public void LogWarning(string message)
    {
        WarningLogs.Add(message);
    }

    public void LogError(string message)
    {
        ErrorLogs.Add(message);
    }

    public void LogError(Exception exception)
    {
        Errors.Add(exception);
    }
}

/// <summary>
/// テスト用のモック IDateTimeProvider（時刻変更可能）
/// </summary>
public class MockDateTimeProvider : IDateTimeProvider
{
    private DateTime _now;

    public MockDateTimeProvider(DateTime now)
    {
        _now = now;
    }

    public DateTime Now => _now;

    public void SetNow(DateTime now)
    {
        _now = now;
    }
}

/// <summary>
/// テスト用のモック IRandomGenerator
/// </summary>
public class MockRandomGenerator : IRandomGenerator
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
public class MockToolFunction : ToolFunction
{
    private record Parameters();
    private readonly bool _canBeLazy;

    public MockToolFunction(bool canBeLazy, ILogger logger) : base(logger)
    {
        _canBeLazy = canBeLazy;
    }

    public override bool CanBeLazy => _canBeLazy;
    public override string Description => "Mock function";
    protected override Type ParameterType => typeof(Parameters);

    public override Task<string?> Invoke(JsonDocument argumentsDoc, ConversationState state)
    {
        return Task.FromResult<string?>("{}");
    }
}

/// <summary>
/// テスト用のモック PipelineResponse（ClientResult作成に必要）
/// </summary>
public class MockPipelineResponse : PipelineResponse
{
    public override int Status => 200;
    public override string ReasonPhrase => "OK";
    public override Stream? ContentStream { get; set; }
    public override BinaryData Content => BinaryData.FromString("{}");

    protected override PipelineResponseHeaders HeadersCore => new MockPipelineResponseHeaders();

    public override BinaryData BufferContent(CancellationToken cancellationToken = default)
    {
        return Content;
    }

    public override ValueTask<BinaryData> BufferContentAsync(CancellationToken cancellationToken = default)
    {
        return new ValueTask<BinaryData>(Content);
    }

    public override void Dispose() { }
}

/// <summary>
/// テスト用のモック PipelineResponseHeaders
/// </summary>
public class MockPipelineResponseHeaders : PipelineResponseHeaders
{
    private readonly Dictionary<string, string> _headers = new();

    public override IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return _headers.GetEnumerator();
    }

    public override bool TryGetValue(string name, out string? value)
    {
        return _headers.TryGetValue(name, out value);
    }

    public override bool TryGetValues(string name, out IEnumerable<string>? values)
    {
        if (_headers.TryGetValue(name, out var value))
        {
            values = new[] { value };
            return true;
        }
        values = null;
        return false;
    }
}

/// <summary>
/// テスト用のモック IChatMessageRepository
/// </summary>
public class MockChatMessageRepository : IChatMessageRepository
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
public class MockChatCompletionRepository : IChatCompletionRepository
{
    public Func<IEnumerable<ChatMessage>, ChatCompletionOptions?, Task<ChatCompletion>>? CompleteChatAsyncFunc { get; set; }

    public async Task<System.ClientModel.ClientResult<ChatCompletion>> CompleteChatAsync(IEnumerable<ChatMessage> messages, ChatCompletionOptions? options = null)
    {
        if (CompleteChatAsyncFunc != null)
        {
            var completion = await CompleteChatAsyncFunc(messages, options);
            return System.ClientModel.ClientResult.FromValue(completion, new MockPipelineResponse());
        }

        throw new NotImplementedException("CompleteChatAsyncFunc is not set");
    }
}

/// <summary>
/// テスト用のモック IPromptRepository
/// </summary>
public class MockPromptRepository : IPromptRepository
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
    public string InactiveNotification => "非アクティブ通知を送ってください";
}

/// <summary>
/// テスト用のモック ILineMessagingRepository
/// </summary>
public class MockLineMessagingRepository : ILineMessagingRepository
{
    public List<(string userId, string message)> SentMessages { get; } = [];

    public Task SendTextMessageAsync(string userId, string message)
    {
        SentMessages.Add((userId, message));
        return Task.CompletedTask;
    }
}
