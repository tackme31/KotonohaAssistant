using FluentAssertions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.AI.Tests.Helpers;
using KotonohaAssistant.AI.Functions;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Tests.Services;

public class InactivityNotificationServiceTests
{
    #region Fields

    private readonly TestMockChatMessageRepository _chatMessageRepository;
    private readonly MockChatCompletionRepository _chatCompletionRepository;
    private readonly MockPromptRepository _promptRepository;
    private readonly MockLogger _logger;
    private readonly MockLineMessagingRepository _lineMessagingRepository;
    private readonly List<ToolFunction> _availableFunctions;

    #endregion

    #region Constructor

    public InactivityNotificationServiceTests()
    {
        _chatMessageRepository = new TestMockChatMessageRepository();
        _chatCompletionRepository = new MockChatCompletionRepository();
        _promptRepository = new MockPromptRepository();
        _logger = new MockLogger();
        _lineMessagingRepository = new MockLineMessagingRepository();
        _availableFunctions = [];
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// InactivityNotificationServiceTests専用のモック IChatMessageRepository
    /// （メッセージ追加機能付き）
    /// </summary>
    private class TestMockChatMessageRepository : MockChatMessageRepository
    {
        private readonly List<Message> _messages = [];
        private readonly long _conversationId = 1;

        public TestMockChatMessageRepository()
        {
            // 基底クラスのFuncプロパティを設定して、インターフェース経由でも正しく動作するようにする
            GetLatestConversationIdAsyncFunc = () => Task.FromResult(_conversationId);
            CreateNewConversationIdAsyncFunc = () => Task.FromResult((int)_conversationId);
            GetAllMessageAsyncFunc = (conversationId) => Task.FromResult<IEnumerable<Message>>(_messages);
            GetAllChatMessagesAsyncFunc = (conversationId) =>
            {
                var chatMessages = _messages.Select(m => m.ToChatMessage()).ToList();
                return Task.FromResult<IEnumerable<ChatMessage>>(chatMessages);
            };
        }

        public void AddMessage(Message message)
        {
            _messages.Add(message);
        }
    }

    #endregion

    #region Start メソッドのスケジューリングテスト

    [Fact]
    public void Start_notifyTimeが現在時刻より前の場合_翌日にスケジュールされること()
    {
        // Arrange
        // 現在時刻を 12:00 に設定
        var now = new DateTime(2025, 1, 1, 12, 0, 0);
        var dateTimeProvider = new Helpers.MockDateTimeProvider(now);

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
        var dateTimeProvider = new Helpers.MockDateTimeProvider(now);

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
        var dateTimeProvider = new Helpers.MockDateTimeProvider(now);

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
        var dateTimeProvider = new Helpers.MockDateTimeProvider(now);

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
        var completionResponse = ChatCompletionFactory.CreateRawTextCompletion(notificationResponseJson);
        var chatCompletionRepository = new Helpers.MockChatCompletionRepository()
        {
            CompleteChatAsyncFunc = (messages, options) => Task.FromResult(completionResponse)
        };

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
        // Arrange
        // 現在時刻を 12:00 に固定
        var now = new DateTime(2025, 1, 1, 12, 0, 0);
        var thirtyMinutesAgo = now.AddMinutes(-30);
        var dateTimeProvider = new Helpers.MockDateTimeProvider(now);

        // 30分前の茜のメッセージを追加
        var chatResponseJson = """{"Assistant":"Akane","Text":"こんにちは"}""";
        var message = new Message
        {
            Id = 1,
            ConversationId = 1,
            Type = "Assistant",
            Content = System.Text.Json.JsonSerializer.Serialize(new[] { chatResponseJson }),
            ToolCalls = "[]",
            CreatedAt = thirtyMinutesAgo
        };
        _chatMessageRepository.AddMessage(message);

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

        // Act
        // notifyInterval (1時間) を超えていないので通知は送信されないはず
        var notifyInterval = TimeSpan.FromHours(1);
        var checkInactivityMethod = typeof(InactivityNotificationService)
            .GetMethod("CheckInactivityAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        await (Task)checkInactivityMethod!.Invoke(service, [notifyInterval])!;

        // Assert
        // LINE 通知が送信されていないことを検証
        _lineMessagingRepository.SentMessages.Should().BeEmpty();

        // ログに「Not enough time elapsed. No notification.」が出力されることを検証
        _logger.InformationLogs.Should().Contain(log => log.Contains("Not enough time elapsed. No notification."));
    }

    [Fact]
    public async Task CheckInactivityAsync_アクティビティが見つからない_通知をスキップすること()
    {
        // Arrange
        var now = new DateTime(2025, 1, 1, 12, 0, 0);
        var dateTimeProvider = new Helpers.MockDateTimeProvider(now);

        // メッセージを追加しない（空のリポジトリ）

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

        // Act
        var notifyInterval = TimeSpan.FromHours(1);
        var checkInactivityMethod = typeof(InactivityNotificationService)
            .GetMethod("CheckInactivityAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        await (Task)checkInactivityMethod!.Invoke(service, [notifyInterval])!;

        // Assert
        // LINE 通知が送信されていないことを検証
        _lineMessagingRepository.SentMessages.Should().BeEmpty();

        // ログに「No activity found. Skipping.」が出力されることを検証
        _logger.WarningLogs.Should().Contain(log => log.Contains("No activity found. Skipping."));
    }

    [Fact]
    public async Task CheckInactivityAsync_通知メッセージが空_通知をスキップすること()
    {
        // Arrange
        var now = new DateTime(2025, 1, 1, 12, 0, 0);
        var twoHoursAgo = now.AddHours(-2);
        var dateTimeProvider = new Helpers.MockDateTimeProvider(now);

        // 2時間前の茜のメッセージを追加
        var chatResponseJson = """{"Assistant":"Akane","Text":"こんにちは"}""";
        var message = new Message
        {
            Id = 1,
            ConversationId = 1,
            Type = "Assistant",
            Content = System.Text.Json.JsonSerializer.Serialize(new[] { chatResponseJson }),
            ToolCalls = "[]",
            CreatedAt = twoHoursAgo
        };
        _chatMessageRepository.AddMessage(message);

        // AI が空のテキストを返すようにモック
        var emptyResponseJson = """{"Assistant":"Akane","Text":""}""";
        var completionResponse = ChatCompletionFactory.CreateRawTextCompletion(emptyResponseJson);
        var chatCompletionRepository = new Helpers.MockChatCompletionRepository()
        {
            CompleteChatAsyncFunc = (messages, options) => Task.FromResult(completionResponse)
        };

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
        var notifyInterval = TimeSpan.FromHours(1);
        var checkInactivityMethod = typeof(InactivityNotificationService)
            .GetMethod("CheckInactivityAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        await (Task)checkInactivityMethod!.Invoke(service, [notifyInterval])!;

        // Assert
        // LINE 通知が送信されていないことを検証
        _lineMessagingRepository.SentMessages.Should().BeEmpty();

        // ログに「Generated message is empty. Skipping notification.」が出力されることを検証
        _logger.WarningLogs.Should().Contain(log => log.Contains("Generated message is empty. Skipping notification."));
    }

    [Fact]
    public async Task CheckInactivityAsync_ChatResponseパース失敗_通知をスキップすること()
    {
        // Arrange
        var now = new DateTime(2025, 1, 1, 12, 0, 0);
        var twoHoursAgo = now.AddHours(-2);
        var dateTimeProvider = new Helpers.MockDateTimeProvider(now);

        // ChatResponse としてパースできない無効なメッセージを追加
        var invalidJson = """not a json at all""";
        var message = new Message
        {
            Id = 1,
            ConversationId = 1,
            Type = "Assistant",
            Content = System.Text.Json.JsonSerializer.Serialize(new[] { invalidJson }),
            ToolCalls = "[]",
            CreatedAt = twoHoursAgo
        };
        _chatMessageRepository.AddMessage(message);

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

        // Act
        var notifyInterval = TimeSpan.FromHours(1);
        var checkInactivityMethod = typeof(InactivityNotificationService)
            .GetMethod("CheckInactivityAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        await (Task)checkInactivityMethod!.Invoke(service, [notifyInterval])!;

        // Assert
        // LINE 通知が送信されていないことを検証
        _lineMessagingRepository.SentMessages.Should().BeEmpty();

        // ログに「No activity found. Skipping.」が出力されることを検証
        _logger.WarningLogs.Should().Contain(log => log.Contains("No activity found. Skipping."));
    }

    [Fact]
    public async Task CheckInactivityAsync_通知生成時のChatResponseパース失敗_通知をスキップすること()
    {
        // Arrange
        var now = new DateTime(2025, 1, 1, 12, 0, 0);
        var twoHoursAgo = now.AddHours(-2);
        var dateTimeProvider = new Helpers.MockDateTimeProvider(now);

        // 2時間前の茜のメッセージを追加（有効なメッセージ）
        var chatResponseJson = """{"Assistant":"Akane","Text":"こんにちは"}""";
        var message = new Message
        {
            Id = 1,
            ConversationId = 1,
            Type = "Assistant",
            Content = System.Text.Json.JsonSerializer.Serialize(new[] { chatResponseJson }),
            ToolCalls = "[]",
            CreatedAt = twoHoursAgo
        };
        _chatMessageRepository.AddMessage(message);

        // AI が無効なフォーマットの応答を返すようにモック
        var invalidResponseJson = """not a json at all""";
        var completionResponse = ChatCompletionFactory.CreateRawTextCompletion(invalidResponseJson);
        var chatCompletionRepository = new Helpers.MockChatCompletionRepository()
        {
            CompleteChatAsyncFunc = (messages, options) => Task.FromResult(completionResponse)
        };

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
        var notifyInterval = TimeSpan.FromHours(1);
        var checkInactivityMethod = typeof(InactivityNotificationService)
            .GetMethod("CheckInactivityAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        await (Task)checkInactivityMethod!.Invoke(service, [notifyInterval])!;

        // Assert
        // LINE 通知が送信されていないことを検証
        _lineMessagingRepository.SentMessages.Should().BeEmpty();

        // ログに「The response couldn't be parsed to ChatResponse」が出力されることを検証
        _logger.WarningLogs.Should().Contain(log => log.Contains("The response couldn't be parsed to ChatResponse"));
    }

    [Fact]
    public async Task CheckInactivityAsync_通知生成時に例外発生_通知をスキップすること()
    {
        // Arrange
        var now = new DateTime(2025, 1, 1, 12, 0, 0);
        var twoHoursAgo = now.AddHours(-2);
        var dateTimeProvider = new Helpers.MockDateTimeProvider(now);

        // 2時間前の茜のメッセージを追加
        var chatResponseJson = """{"Assistant":"Akane","Text":"こんにちは"}""";
        var message = new Message
        {
            Id = 1,
            ConversationId = 1,
            Type = "Assistant",
            Content = System.Text.Json.JsonSerializer.Serialize(new[] { chatResponseJson }),
            ToolCalls = "[]",
            CreatedAt = twoHoursAgo
        };
        _chatMessageRepository.AddMessage(message);

        // ChatCompletionRepository が例外をスローするようにモック
        var chatCompletionRepository = new Helpers.MockChatCompletionRepository()
        {
            CompleteChatAsyncFunc = (messages, options) => throw new InvalidOperationException("Test exception")
        };

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
        var notifyInterval = TimeSpan.FromHours(1);
        var checkInactivityMethod = typeof(InactivityNotificationService)
            .GetMethod("CheckInactivityAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        await (Task)checkInactivityMethod!.Invoke(service, [notifyInterval])!;

        // Assert
        // LINE 通知が送信されていないことを検証
        _lineMessagingRepository.SentMessages.Should().BeEmpty();

        // Logger.LogError(Exception) が呼ばれることを検証
        _logger.Errors.Should().HaveCount(1);
        _logger.Errors[0].Should().BeOfType<InvalidOperationException>();
        _logger.Errors[0].Message.Should().Be("Test exception");
    }

    #endregion

    #region SendInactivityNotificationAsync 正常系テスト

    [Fact(Skip ="クリティカルでないため一旦スキップ")]
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

    [Fact(Skip = "クリティカルでないため一旦スキップ")]
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

    [Fact(Skip = "クリティカルでないため一旦スキップ")]
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

    [Fact(Skip = "クリティカルでないため一旦スキップ")]
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

    [Fact(Skip = "クリティカルでないため一旦スキップ")]
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

    [Fact(Skip = "クリティカルでないため一旦スキップ")]
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

    [Fact(Skip = "クリティカルでないため一旦スキップ")]
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

    [Fact(Skip = "クリティカルでないため一旦スキップ")]
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

    [Fact(Skip = "クリティカルでないため一旦スキップ")]
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

    [Fact(Skip = "クリティカルでないため一旦スキップ")]
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

    [Fact(Skip = "クリティカルでないため一旦スキップ")]
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

    [Fact(Skip = "クリティカルでないため一旦スキップ")]
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

    [Fact(Skip = "クリティカルでないため一旦スキップ")]
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
