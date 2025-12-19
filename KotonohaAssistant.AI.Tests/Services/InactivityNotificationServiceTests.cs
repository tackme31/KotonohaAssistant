using FluentAssertions;
using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Utils;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Tests.Services;

public class InactivityNotificationServiceTests
{
    #region Fields

    private readonly MockChatMessageRepository _chatMessageRepository;
    private readonly MockChatCompletionRepository _chatCompletionRepository;
    private readonly MockPromptRepository _promptRepository;
    private readonly MockLogger _logger;
    private readonly MockLineMessagingRepository _lineMessagingRepository;
    private readonly List<ToolFunction> _availableFunctions;

    #endregion

    #region Constructor

    public InactivityNotificationServiceTests()
    {
        _chatMessageRepository = new MockChatMessageRepository();
        _chatCompletionRepository = new MockChatCompletionRepository();
        _promptRepository = new MockPromptRepository();
        _logger = new MockLogger();
        _lineMessagingRepository = new MockLineMessagingRepository();
        _availableFunctions = new List<ToolFunction>();
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// テスト用のモック IDateTimeProvider
    /// </summary>
    private class MockDateTimeProvider : IDateTimeProvider
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
    /// テスト用のモック IChatMessageRepository
    /// </summary>
    private class MockChatMessageRepository : IChatMessageRepository
    {
        private readonly List<Message> _messages = [];
        private readonly long _conversationId = 1;

        public void AddMessage(Message message)
        {
            _messages.Add(message);
        }

        public Task<long> GetLatestConversationIdAsync()
        {
            return Task.FromResult(_conversationId);
        }

        public Task<int> CreateNewConversationIdAsync()
        {
            return Task.FromResult((int)_conversationId);
        }

        public Task<IEnumerable<Message>> GetAllMessageAsync(long conversationId)
        {
            return Task.FromResult<IEnumerable<Message>>(_messages);
        }

        public Task<IEnumerable<ChatMessage>> GetAllChatMessagesAsync(long conversationId)
        {
            var chatMessages = _messages.Select(m => m.ToChatMessage()).ToList();
            return Task.FromResult<IEnumerable<ChatMessage>>(chatMessages);
        }

        public Task InsertChatMessagesAsync(IEnumerable<ChatMessage> chatMessages, long conversationId)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// テスト用のモック IChatCompletionRepository
    /// </summary>
    private class MockChatCompletionRepository : IChatCompletionRepository
    {
        private readonly Func<IEnumerable<ChatMessage>, ChatCompletionOptions, Task<ChatCompletion?>>? _completionFunc;

        public MockChatCompletionRepository(
            Func<IEnumerable<ChatMessage>, ChatCompletionOptions, Task<ChatCompletion?>>? completionFunc = null)
        {
            _completionFunc = completionFunc;
        }

        public async Task<System.ClientModel.ClientResult<ChatCompletion>> CompleteChatAsync(
            IEnumerable<ChatMessage> messages,
            ChatCompletionOptions? options = null)
        {
            if (_completionFunc != null)
            {
                var result = await _completionFunc(messages, options ?? new ChatCompletionOptions());
                if (result != null)
                {
                    return System.ClientModel.ClientResult.FromValue(result, new MockPipelineResponse());
                }
            }
            throw new InvalidOperationException("No completion result available");
        }
    }

    /// <summary>
    /// テスト用のモック PipelineResponse
    /// </summary>
    private class MockPipelineResponse : System.ClientModel.Primitives.PipelineResponse
    {
        public override int Status => 200;
        public override string ReasonPhrase => "OK";
        public override Stream? ContentStream { get; set; }
        public override BinaryData Content => BinaryData.FromString("{}");

        protected override System.ClientModel.Primitives.PipelineResponseHeaders HeadersCore => new MockPipelineResponseHeaders();

        public override BinaryData BufferContent(CancellationToken cancellationToken = default)
        {
            return Content;
        }

        public override async ValueTask<BinaryData> BufferContentAsync(CancellationToken cancellationToken = default)
        {
            return await ValueTask.FromResult(Content);
        }

        public override void Dispose() { }
    }

    /// <summary>
    /// テスト用のモック PipelineResponseHeaders
    /// </summary>
    private class MockPipelineResponseHeaders : System.ClientModel.Primitives.PipelineResponseHeaders
    {
        public override IEnumerator<KeyValuePair<string, string>> GetEnumerator() =>
            new List<KeyValuePair<string, string>>().GetEnumerator();

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

        public string MakeTimeBasedPromise => "MakeTimeBasedPromise description";
        public string CreateCalendarEventDescription => "CreateCalendarEvent description";
        public string ForgetMemoryDescription => "ForgetMemory description";
        public string GetCalendarEventDescription => "GetCalendarEvent description";
        public string GetWeatherDescription => "GetWeather description";
        public string StartTimerDescription => "StartTimer description";
        public string StopAlarmDescription => "StopAlarm description";
        public string StopTimerDescription => "StopTimer description";
        public string InactiveNotification => "非アクティブ通知を送ってください";
    }

    /// <summary>
    /// テスト用のモック ILogger
    /// </summary>
    private class MockLogger : ILogger
    {
        public List<string> InformationLogs { get; } = [];
        public List<string> WarningLogs { get; } = [];
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
            // Not used in tests
        }

        public void LogError(Exception exception)
        {
            Errors.Add(exception);
        }
    }

    /// <summary>
    /// テスト用のモック ILineMessagingRepository
    /// </summary>
    private class MockLineMessagingRepository : ILineMessagingRepository
    {
        public List<(string userId, string message)> SentMessages { get; } = [];

        public Task SendTextMessageAsync(string userId, string message)
        {
            SentMessages.Add((userId, message));
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// テスト用の ChatCompletion を作成（テキスト応答）
    /// </summary>
    private ChatCompletion CreateTextCompletion(string text)
    {
        return OpenAIChatModelFactory.ChatCompletion(
            id: "test-id",
            model: "gpt-4",
            createdAt: DateTimeOffset.Now,
            finishReason: ChatFinishReason.Stop,
            role: ChatMessageRole.Assistant,
            content: [ChatMessageContentPart.CreateTextPart(text)]
        );
    }

    #endregion

    #region Start メソッドのスケジューリングテスト

    [Fact]
    public void Start_notifyTimeが現在時刻より前の場合_翌日にスケジュールされること()
    {
        // Arrange
        // 現在時刻を 12:00 に設定
        var now = new DateTime(2025, 1, 1, 12, 0, 0);
        var dateTimeProvider = new MockDateTimeProvider(now);

        var service = new InactivityNotificationService(
            _chatMessageRepository,
            _chatCompletionRepository,
            _availableFunctions,
            _promptRepository,
            _logger,
            _lineMessagingRepository,
            dateTimeProvider,
            "test-user-id"
        );

        // notifyTime を 09:00 (現在時刻より前) に設定
        var notifyTime = TimeSpan.FromHours(9);
        var notifyInterval = TimeSpan.FromHours(1);

        // Act
        service.Start(notifyInterval, notifyTime);

        // Assert
        // 翌日の 09:00 がスケジュールされることを確認
        var expectedNextRun = new DateTime(2025, 1, 2, 9, 0, 0);
        _logger.InformationLogs.Should().Contain(log =>
            log.Contains("Next check scheduled at") &&
            log.Contains(expectedNextRun.ToString()));
    }

    [Fact]
    public void Start_notifyTimeが現在時刻より後の場合_当日にスケジュールされること()
    {
        // Arrange
        // 現在時刻を 12:00 に設定
        var now = new DateTime(2025, 1, 1, 12, 0, 0);
        var dateTimeProvider = new MockDateTimeProvider(now);

        var service = new InactivityNotificationService(
            _chatMessageRepository,
            _chatCompletionRepository,
            _availableFunctions,
            _promptRepository,
            _logger,
            _lineMessagingRepository,
            dateTimeProvider,
            "test-user-id"
        );

        // notifyTime を 15:00 (現在時刻より後) に設定
        var notifyTime = TimeSpan.FromHours(15);
        var notifyInterval = TimeSpan.FromHours(1);

        // Act
        service.Start(notifyInterval, notifyTime);

        // Assert
        // 当日の 15:00 がスケジュールされることを確認
        var expectedNextRun = new DateTime(2025, 1, 1, 15, 0, 0);
        _logger.InformationLogs.Should().Contain(log =>
            log.Contains("Next check scheduled at") &&
            log.Contains(expectedNextRun.ToString()));
    }

    [Fact]
    public void Start_複数回呼び出し_既存のタイマーが破棄されること()
    {
        // Arrange
        var now = new DateTime(2025, 1, 1, 12, 0, 0);
        var dateTimeProvider = new MockDateTimeProvider(now);

        var service = new InactivityNotificationService(
            _chatMessageRepository,
            _chatCompletionRepository,
            _availableFunctions,
            _promptRepository,
            _logger,
            _lineMessagingRepository,
            dateTimeProvider,
            "test-user-id"
        );

        var notifyTime1 = TimeSpan.FromHours(15);
        var notifyTime2 = TimeSpan.FromHours(18);
        var notifyInterval = TimeSpan.FromHours(1);

        // Act
        // 1回目のStart呼び出し
        service.Start(notifyInterval, notifyTime1);
        var logCountAfterFirst = _logger.InformationLogs.Count;

        // 2回目のStart呼び出し (既存のタイマーが破棄される)
        service.Start(notifyInterval, notifyTime2);
        var logCountAfterSecond = _logger.InformationLogs.Count;

        // Assert
        // 例外が発生しないこと
        logCountAfterFirst.Should().Be(1);
        logCountAfterSecond.Should().Be(2);

        // 最後にスケジュールされたのが18:00であることを確認
        var expectedNextRun = new DateTime(2025, 1, 1, 18, 0, 0);
        _logger.InformationLogs[1].Should().Contain("Next check scheduled at");
        _logger.InformationLogs[1].Should().Contain(expectedNextRun.ToString());
    }

    #endregion

    #region CheckInactivityAsync テスト

    [Fact]
    public async Task CheckInactivityAsync_非アクティブ期間経過_LINE通知が送信されること()
    {
        // Arrange
        // 現在時刻を 12:00 に固定
        var now = new DateTime(2025, 1, 1, 12, 0, 0);
        var twoHoursAgo = now.AddHours(-2);
        var dateTimeProvider = new MockDateTimeProvider(now);

        // 2時間前の茜のメッセージを追加
        var chatResponseJson = """{"Assistant":"Akane","Text":"こんにちは"}""";
        var message = new Message
        {
            Id = 1,
            ConversationId = 1,
            Type = "Assistant",
            // Content は文字列の配列を JSON シリアライズしたもの（データベース保存形式）
            Content = System.Text.Json.JsonSerializer.Serialize(new[] { chatResponseJson }),
            ToolCalls = "[]",
            CreatedAt = twoHoursAgo
        };
        _chatMessageRepository.AddMessage(message);

        // AI が生成する通知メッセージをモック
        var notificationResponseJson = """{"Assistant":"Akane","Text":"久しぶりやな！"}""";
        var completionResponse = CreateTextCompletion(notificationResponseJson);
        var chatCompletionRepository = new MockChatCompletionRepository(
            (messages, options) => Task.FromResult<ChatCompletion?>(completionResponse)
        );

        var service = new InactivityNotificationService(
            _chatMessageRepository,
            chatCompletionRepository,
            _availableFunctions,
            _promptRepository,
            _logger,
            _lineMessagingRepository,
            dateTimeProvider,
            "test-user-id"
        );

        // Act
        // notifyInterval (1時間) を超えているので通知が送信されるはず
        var notifyInterval = TimeSpan.FromHours(1);
        var checkInactivityMethod = typeof(InactivityNotificationService)
            .GetMethod("CheckInactivityAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        await (Task)checkInactivityMethod!.Invoke(service, [notifyInterval])!;

        // Assert
        // LINE 通知が送信されたことを検証
        _lineMessagingRepository.SentMessages.Should().HaveCount(1);
        _lineMessagingRepository.SentMessages[0].userId.Should().Be("test-user-id");
        _lineMessagingRepository.SentMessages[0].message.Should().Be("久しぶりやな！");

        _logger.InformationLogs.Should().Contain(log => log.Contains("Sending inactivity reminder"));
    }

    [Fact]
    public async Task CheckInactivityAsync_非アクティブ期間未経過_通知が送信されないこと()
    {
        // テスト内容:
        // - 最後のアクティビティが 30 分前
        // - notifyInterval が 1 時間の場合
        // - LINE 通知が送信されないこと
        //
        // 期待される値:
        // - LineMessagingRepository.SendTextMessageAsync が呼ばれない
        // - ログに「Not enough time elapsed. No notification.」が出力される

        throw new NotImplementedException();
    }

    [Fact]
    public async Task CheckInactivityAsync_アクティビティが見つからない_通知をスキップすること()
    {
        // テスト内容:
        // - ChatMessageRepository が空のメッセージリストを返す
        // - 通知がスキップされること
        //
        // 期待される値:
        // - LineMessagingRepository.SendTextMessageAsync が呼ばれない
        // - ログに「No activity found. Skipping.」が出力される

        throw new NotImplementedException();
    }

    [Fact]
    public async Task CheckInactivityAsync_通知メッセージが空_通知をスキップすること()
    {
        // テスト内容:
        // - ChatCompletionRepository が空のテキストを返す
        // - 通知がスキップされること
        //
        // 期待される値:
        // - LineMessagingRepository.SendTextMessageAsync が呼ばれない
        // - ログに「Generated message is empty. Skipping notification.」が出力される

        throw new NotImplementedException();
    }

    [Fact]
    public async Task CheckInactivityAsync_ChatResponseパース失敗_通知をスキップすること()
    {
        // テスト内容:
        // - 最後のアクティビティのメッセージが ChatResponse としてパースできない
        // - GetLastActivity が null を返すこと
        // - 通知がスキップされること
        //
        // 期待される値:
        // - LineMessagingRepository.SendTextMessageAsync が呼ばれない
        // - ログに「No activity found. Skipping.」が出力される

        throw new NotImplementedException();
    }

    [Fact]
    public async Task CheckInactivityAsync_通知生成時のChatResponseパース失敗_通知をスキップすること()
    {
        // テスト内容:
        // - SendInactivityNotificationAsync 内で生成された通知メッセージが
        //   ChatResponse としてパースできない
        // - 通知がスキップされること
        //
        // 期待される値:
        // - LineMessagingRepository.SendTextMessageAsync が呼ばれない
        // - ログに「The response couldn't be parsed to ChatResponse」が出力される

        throw new NotImplementedException();
    }

    [Fact]
    public async Task CheckInactivityAsync_通知生成時に例外発生_通知をスキップすること()
    {
        // テスト内容:
        // - ChatCompletionRepository.CompleteChatAsync が例外をスローする
        // - 通知がスキップされること
        //
        // 期待される値:
        // - LineMessagingRepository.SendTextMessageAsync が呼ばれない
        // - Logger.LogError(Exception) が呼ばれる

        throw new NotImplementedException();
    }

    #endregion

    #region SendInactivityNotificationAsync 正常系テスト

    [Fact]
    public async Task SendInactivityNotificationAsync_茜の最終アクティビティ_茜として通知を生成すること()
    {
        // テスト内容:
        // - 最後のアクティビティが茜のメッセージ
        // - 茜として通知メッセージが生成されること
        //
        // 期待される値:
        // - CompleteChatAsync に渡される state.CurrentSister が Kotonoha.Akane
        // - InactiveNotification instruction が追加されていること
        // - SwitchSisterTo(Akane) instruction が追加されていること

        throw new NotImplementedException();
    }

    [Fact]
    public async Task SendInactivityNotificationAsync_葵の最終アクティビティ_葵として通知を生成すること()
    {
        // テスト内容:
        // - 最後のアクティビティが葵のメッセージ
        // - 葵として通知メッセージが生成されること
        //
        // 期待される値:
        // - CompleteChatAsync に渡される state.CurrentSister が Kotonoha.Aoi
        // - InactiveNotification instruction が追加されていること
        // - SwitchSisterTo(Aoi) instruction が追加されていること

        throw new NotImplementedException();
    }

    [Fact]
    public async Task SendInactivityNotificationAsync_最近のメッセージのみ使用_最大20件に制限されること()
    {
        // テスト内容:
        // - ChatMessageRepository が 30 件のメッセージを返す
        // - CompleteChatAsync に渡されるメッセージが最新 20 件に制限されること
        //
        // 期待される値:
        // - CompleteChatAsync に渡される messages のうち、
        //   ユーザー/アシスタントメッセージが最大 20 件であること

        throw new NotImplementedException();
    }

    [Fact]
    public async Task SendInactivityNotificationAsync_ToolChatMessageをスキップ_先頭のToolChatMessageが除外されること()
    {
        // テスト内容:
        // - 最新 20 件のメッセージの先頭に ToolChatMessage が含まれる
        // - ToolChatMessage が SkipWhile でスキップされること
        //
        // 期待される値:
        // - CompleteChatAsync に渡される messages に ToolChatMessage が含まれないこと
        // - 先頭の連続した ToolChatMessage のみがスキップされること

        throw new NotImplementedException();
    }

    #endregion

    #region RunAndRescheduleAsync テスト

    [Fact]
    public async Task RunAndRescheduleAsync_例外発生_タイマーが再スケジュールされること()
    {
        // テスト内容:
        // - CheckInactivityAsync 内で例外が発生する
        // - finally ブロックで ScheduleNextRun が呼ばれること
        //
        // 期待される値:
        // - Logger.LogError(Exception) が呼ばれる
        // - ログに「Next check scheduled at」が出力される（再スケジュールの証明）

        throw new NotImplementedException();
    }

    [Fact]
    public async Task RunAndRescheduleAsync_正常終了_タイマーが再スケジュールされること()
    {
        // テスト内容:
        // - CheckInactivityAsync が正常に完了する
        // - finally ブロックで ScheduleNextRun が呼ばれること
        //
        // 期待される値:
        // - ログに「Next check scheduled at」が出力される（再スケジュールの証明）
        // - 例外が発生しないこと

        throw new NotImplementedException();
    }

    #endregion

    #region GetLastActivity テスト

    [Fact]
    public void GetLastActivity_有効なメッセージが存在_最新のChatResponseを返すこと()
    {
        // テスト内容:
        // - メッセージリストに有効な ChatResponse を含むメッセージが複数存在
        // - 最新のメッセージから順に検索し、最初に見つかった ChatResponse を返すこと
        //
        // 期待される値:
        // - 返り値が null でないこと
        // - createdAt, sister, conversationId が正しく設定されていること
        // - 最新のメッセージが優先されること

        throw new NotImplementedException();
    }

    [Fact]
    public void GetLastActivity_空のContentを持つメッセージ_スキップすること()
    {
        // テスト内容:
        // - Content が空配列のメッセージが含まれる
        // - そのメッセージがスキップされ、次のメッセージが評価されること
        //
        // 期待される値:
        // - 空の Content を持つメッセージがスキップされる
        // - 有効なメッセージが見つかれば、それが返される

        throw new NotImplementedException();
    }

    [Fact]
    public void GetLastActivity_ChatResponseとしてパースできないメッセージ_スキップすること()
    {
        // テスト内容:
        // - ChatResponse.TryParse が false を返すメッセージが含まれる
        // - そのメッセージがスキップされ、次のメッセージが評価されること
        //
        // 期待される値:
        // - パースできないメッセージがスキップされる
        // - 有効なメッセージが見つかれば、それが返される

        throw new NotImplementedException();
    }

    [Fact]
    public void GetLastActivity_有効なメッセージが存在しない_nullを返すこと()
    {
        // テスト内容:
        // - すべてのメッセージが無効（空の Content、パース失敗など）
        // - null を返すこと
        //
        // 期待される値:
        // - 返り値が null であること

        throw new NotImplementedException();
    }

    #endregion

    #region Dispose テスト

    [Fact]
    public void Dispose_タイマーが破棄されること()
    {
        // テスト内容:
        // - Start メソッドでタイマーを開始した後、Dispose を呼ぶ
        // - タイマーが破棄され、例外が発生しないこと
        //
        // 期待される値:
        // - 例外が発生しないこと
        // - Dispose を複数回呼んでも安全であること

        throw new NotImplementedException();
    }

    [Fact]
    public void Dispose_タイマー開始前の呼び出し_例外が発生しないこと()
    {
        // テスト内容:
        // - Start メソッドを呼ばずに Dispose を呼ぶ
        // - 例外が発生しないこと
        //
        // 期待される値:
        // - 例外が発生しないこと

        throw new NotImplementedException();
    }

    #endregion

    #region 統合テスト（短い間隔での実際のタイマー動作）

    [Fact]
    public async Task Integration_短い間隔でタイマー実行_通知が送信されること()
    {
        // テスト内容:
        // - notifyInterval を 1 秒、notifyTime を現在時刻 + 2秒に設定
        // - Start を呼び出し、3 秒待機
        // - タイマーが実行され、CheckInactivityAsync が呼ばれること
        //
        // 期待される値:
        // - 待機後、ログに「Checking user inactivity...」が出力される
        // - 条件に応じて通知が送信される、またはスキップされる

        throw new NotImplementedException();
    }

    #endregion
}
