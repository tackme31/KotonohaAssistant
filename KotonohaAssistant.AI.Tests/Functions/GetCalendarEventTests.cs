using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Extensions;
using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Tests.Helpers;

namespace KotonohaAssistant.AI.Tests.Functions;

public class GetCalendarEventTests
{
    #region TryParseArguments テスト

    /// <summary>
    /// テストの目的: TryParseArgumentsが有効な日付形式(yyyy/MM/dd)を正しくパースできること
    /// テストする内容:
    /// - 正しい形式の日付文字列がDateTime型に変換される
    /// - argumentsに"date"キーでDateTime値が設定される
    /// 期待される動作: TryParseArgumentsがtrueを返し、argumentsにDateTime値が格納される
    /// </summary>
    [Fact]
    public void TryParseArguments_有効な日付形式_正しくパースできること()
    {
        // Arrange: GetCalendarEventインスタンスと有効な日付を含むJSONドキュメントを準備
        var mockPromptRepository = new MockPromptRepository();
        var mockCalendarEventRepository = new MockCalendarEventRepository();
        var mockLogger = new MockLogger();
        var getCalendarEvent = new GetCalendarEvent(mockPromptRepository, mockCalendarEventRepository, mockLogger);

        var jsonString = """{"date": "2025/01/15"}""";
        using var doc = JsonDocument.Parse(jsonString);

        // Act: TryParseArgumentsを呼び出す
        var result = getCalendarEvent.TryParseArguments(doc, out var arguments);

        // Assert:
        //   - result == true
        //   - arguments["date"] is DateTime
        //   - DateTime値が2025/01/15であること
        result.Should().BeTrue();
        arguments
            .Should().ContainKey("date")
            .WhoseValue.Should().BeOfType<DateTime>()
            .And.Be(15.January(2025));
    }

    /// <summary>
    /// テストの目的: TryParseArgumentsが無効な日付形式を適切に処理すること
    /// テストする内容:
    /// - 不正な形式の日付文字列を渡した場合の処理
    /// - "invalid-date"のような文字列が渡された場合
    /// 期待される動作: TryParseArgumentsがfalseを返す
    /// </summary>
    [Fact]
    public void TryParseArguments_無効な日付形式_falseを返すこと()
    {
        // Arrange: GetCalendarEventインスタンスと無効な日付形式のJSONドキュメントを準備
        var mockPromptRepository = new MockPromptRepository();
        var mockCalendarEventRepository = new MockCalendarEventRepository();
        var mockLogger = new MockLogger();
        var getCalendarEvent = new GetCalendarEvent(mockPromptRepository, mockCalendarEventRepository, mockLogger);

        var jsonString = """{"date": "invalid-date"}""";
        using var doc = JsonDocument.Parse(jsonString);

        // Act: TryParseArgumentsを呼び出す
        var result = getCalendarEvent.TryParseArguments(doc, out var arguments);

        // Assert: result == false
        result.Should().BeFalse();
    }

    /// <summary>
    /// テストの目的: TryParseArgumentsがdateプロパティが存在しない場合を処理できること
    /// テストする内容:
    /// - dateプロパティが含まれていないJSONドキュメント
    /// - 空のJSONオブジェクト
    /// 期待される動作: TryParseArgumentsがfalseを返す
    /// </summary>
    [Fact]
    public void TryParseArguments_dateプロパティなし_falseを返すこと()
    {
        // Arrange: GetCalendarEventインスタンスとdateプロパティを含まないJSONドキュメントを準備
        var mockPromptRepository = new MockPromptRepository();
        var mockCalendarEventRepository = new MockCalendarEventRepository();
        var mockLogger = new MockLogger();
        var getCalendarEvent = new GetCalendarEvent(mockPromptRepository, mockCalendarEventRepository, mockLogger);

        var jsonString = """{}""";
        using var doc = JsonDocument.Parse(jsonString);

        // Act: TryParseArgumentsを呼び出す
        var result = getCalendarEvent.TryParseArguments(doc, out var arguments);

        // Assert: result == false
        result.Should().BeFalse();
    }

    /// <summary>
    /// テストの目的: TryParseArgumentsがnullのdate値を適切に処理すること
    /// テストする内容:
    /// - dateプロパティにnullが設定されている場合
    /// 期待される動作: TryParseArgumentsがfalseを返す
    /// </summary>
    [Fact]
    public void TryParseArguments_nullのdate値_falseを返すこと()
    {
        // Arrange: GetCalendarEventインスタンスとnullのdate値を含むJSONドキュメントを準備
        var mockPromptRepository = new MockPromptRepository();
        var mockCalendarEventRepository = new MockCalendarEventRepository();
        var mockLogger = new MockLogger();
        var getCalendarEvent = new GetCalendarEvent(mockPromptRepository, mockCalendarEventRepository, mockLogger);

        var jsonString = """{"date": null}""";
        using var doc = JsonDocument.Parse(jsonString);

        // Act: TryParseArgumentsを呼び出す
        var result = getCalendarEvent.TryParseArguments(doc, out var arguments);

        // Assert: result == false
        result.Should().BeFalse();
    }

    #endregion

    #region Invoke テスト - 正常系

    /// <summary>
    /// テストの目的: Invokeが予定がない場合に適切なメッセージを返すこと
    /// テストする内容:
    /// - GetEventsAsyncが空のリストを返す場合
    /// - 返されるメッセージの内容
    /// 期待される動作: "予定はありません。"というメッセージが返される
    /// </summary>
    [Fact]
    public async Task Invoke_予定なし_適切なメッセージを返すこと()
    {
        // Arrange:
        //   - MockCalendarEventRepository (空のリストを返す)
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetCalendarEventインスタンス
        //   - 日付を含むarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockCalendarEventRepository = new MockCalendarEventRepository();
        mockCalendarEventRepository.GetEventsAsyncFunc = (date) =>
            Task.FromResult<IList<Google.Apis.Calendar.v3.Data.Event>>(new List<Google.Apis.Calendar.v3.Data.Event>());
        var mockLogger = new MockLogger();
        var getCalendarEvent = new GetCalendarEvent(mockPromptRepository, mockCalendarEventRepository, mockLogger);

        var arguments = new Dictionary<string, object>
        {
            { "date", new DateTime(2025, 1, 15) }
        };
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getCalendarEvent.Invoke(arguments, state);

        // Assert:
        //   - result == "予定はありません。"
        //   - GetEventsAsyncが1回呼ばれる
        result.Should().Be("予定はありません。");
        mockCalendarEventRepository.GetEventsAsyncCallCount.Should().Be(1);
    }

    /// <summary>
    /// テストの目的: Invokeが通常の予定(開始時刻と終了時刻あり)を正しくフォーマットすること
    /// テストする内容:
    /// - 1つの予定が返される場合
    /// - Start/Endの両方にDateTimeDateTimeOffsetが設定されている
    /// - 予定が今日の範囲内
    /// 期待される動作: "## M月d日の予定\n- [HH:mmからHH:mmまで] サマリー" 形式で返される
    /// </summary>
    [Fact]
    public async Task Invoke_通常の予定_正しくフォーマットされること()
    {
        // Arrange:
        //   - MockCalendarEventRepository (1つのイベントを返す)
        //   - イベントのStart/Endに今日の日時を設定
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetCalendarEventインスタンス
        //   - 日付を含むarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockCalendarEventRepository = new MockCalendarEventRepository();

        var today = DateTime.Today;
        var eventItem = new Google.Apis.Calendar.v3.Data.Event
        {
            Summary = "テストミーティング",
            Start = new Google.Apis.Calendar.v3.Data.EventDateTime
            {
                DateTimeDateTimeOffset = new DateTimeOffset(today.AddHours(10))
            },
            End = new Google.Apis.Calendar.v3.Data.EventDateTime
            {
                DateTimeDateTimeOffset = new DateTimeOffset(today.AddHours(11))
            }
        };

        mockCalendarEventRepository.GetEventsAsyncFunc = (date) =>
            Task.FromResult<IList<Google.Apis.Calendar.v3.Data.Event>>(new List<Google.Apis.Calendar.v3.Data.Event> { eventItem });

        var mockLogger = new MockLogger();
        var getCalendarEvent = new GetCalendarEvent(mockPromptRepository, mockCalendarEventRepository, mockLogger);

        var arguments = new Dictionary<string, object>
        {
            { "date", today }
        };
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getCalendarEvent.Invoke(arguments, state);

        // Assert:
        //   - resultに"## M月d日の予定"が含まれる
        //   - resultに"[HH:mmからHH:mmまで]"が含まれる
        //   - resultにイベントのSummaryが含まれる
        result.Should().Contain($"## {today.Month}月{today.Day}日の予定");
        result.Should().Contain("[10:00から11:00まで]");
        result.Should().Contain("テストミーティング");
    }

    /// <summary>
    /// テストの目的: Invokeが複数の予定を正しく列挙すること
    /// テストする内容:
    /// - 複数のイベントが返される場合
    /// - 各イベントが改行で区切られて表示される
    /// 期待される動作: すべてのイベントが箇条書きで返される
    /// </summary>
    [Fact]
    public async Task Invoke_複数の予定_すべて列挙されること()
    {
        // Arrange:
        //   - MockCalendarEventRepository (複数のイベントを返す)
        //   - 各イベントに異なるSummaryを設定
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetCalendarEventインスタンス
        //   - 日付を含むarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockCalendarEventRepository = new MockCalendarEventRepository();

        var today = DateTime.Today;
        var events = new List<Google.Apis.Calendar.v3.Data.Event>
        {
            new()
            {
                Summary = "朝のミーティング",
                Start = new Google.Apis.Calendar.v3.Data.EventDateTime
                {
                    DateTimeDateTimeOffset = new DateTimeOffset(today.AddHours(9))
                },
                End = new Google.Apis.Calendar.v3.Data.EventDateTime
                {
                    DateTimeDateTimeOffset = new DateTimeOffset(today.AddHours(10))
                }
            },
            new()
            {
                Summary = "ランチ",
                Start = new Google.Apis.Calendar.v3.Data.EventDateTime
                {
                    DateTimeDateTimeOffset = new DateTimeOffset(today.AddHours(12))
                },
                End = new Google.Apis.Calendar.v3.Data.EventDateTime
                {
                    DateTimeDateTimeOffset = new DateTimeOffset(today.AddHours(13))
                }
            },
            new()
            {
                Summary = "プロジェクトレビュー",
                Start = new Google.Apis.Calendar.v3.Data.EventDateTime
                {
                    DateTimeDateTimeOffset = new DateTimeOffset(today.AddHours(15))
                },
                End = new Google.Apis.Calendar.v3.Data.EventDateTime
                {
                    DateTimeDateTimeOffset = new DateTimeOffset(today.AddHours(16))
                }
            }
        };

        mockCalendarEventRepository.GetEventsAsyncFunc = (date) =>
            Task.FromResult<IList<Google.Apis.Calendar.v3.Data.Event>>(events);

        var mockLogger = new MockLogger();
        var getCalendarEvent = new GetCalendarEvent(mockPromptRepository, mockCalendarEventRepository, mockLogger);

        var arguments = new Dictionary<string, object>
        {
            { "date", today }
        };
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getCalendarEvent.Invoke(arguments, state);

        // Assert:
        //   - resultにすべてのイベントのSummaryが含まれる
        //   - "- "で始まる行が複数ある
        result.Should().ContainAll("朝のミーティング", "ランチ", "プロジェクトレビュー");

        // "- "で始まる行が3つあることを確認
        result.Should().Contain("\n- ", Exactly.Thrice());
    }

    /// <summary>
    /// テストの目的: Invokeが終日予定(DateTimeDateTimeOffsetがnull)を正しく処理すること
    /// テストする内容:
    /// - Start/EndのDateTimeDateTimeOffsetがnullの場合
    /// - 時刻表示なしでSummaryのみが表示される
    /// 期待される動作: "- サマリー" 形式で返される
    /// </summary>
    [Fact]
    public async Task Invoke_終日予定_時刻なしで表示されること()
    {
        // Arrange:
        //   - MockCalendarEventRepository (DateTimeDateTimeOffsetがnullのイベントを返す)
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetCalendarEventインスタンス
        //   - 日付を含むarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockCalendarEventRepository = new MockCalendarEventRepository();

        var today = DateTime.Today;
        var eventItem = new Google.Apis.Calendar.v3.Data.Event
        {
            Summary = "終日イベント",
            Start = new Google.Apis.Calendar.v3.Data.EventDateTime
            {
                DateTimeDateTimeOffset = null  // 終日予定
            },
            End = new Google.Apis.Calendar.v3.Data.EventDateTime
            {
                DateTimeDateTimeOffset = null
            }
        };

        mockCalendarEventRepository.GetEventsAsyncFunc = (date) =>
            Task.FromResult<IList<Google.Apis.Calendar.v3.Data.Event>>(new List<Google.Apis.Calendar.v3.Data.Event> { eventItem });

        var mockLogger = new MockLogger();
        var getCalendarEvent = new GetCalendarEvent(mockPromptRepository, mockCalendarEventRepository, mockLogger);

        var arguments = new Dictionary<string, object>
        {
            { "date", today }
        };
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getCalendarEvent.Invoke(arguments, state);

        // Assert:
        //   - resultに時刻の表示([HH:mm])が含まれない
        //   - resultにイベントのSummaryが含まれる
        //   - "- サマリー"形式になっている
        result.Should().Contain("終日イベント");
        result.Should().NotContain("から");
        result.Should().NotContain("まで");
        result.Should().Contain("- 終日イベント");
    }

    /// <summary>
    /// テストの目的: Invokeが今日をまたぐ予定を正しく処理すること
    /// テストする内容:
    /// - 開始時刻が今日より前、終了時刻が今日より後
    /// - 現在時刻が開始と終了の間にある
    /// 期待される動作: "- サマリー" 形式で返される(時刻表示なし)
    /// </summary>
    [Fact]
    public async Task Invoke_今日をまたぐ予定_時刻なしで表示されること()
    {
        // Arrange:
        //   - MockCalendarEventRepository
        //   - MockDateTimeProvider (現在時刻を制御)
        //   - イベントのStartが昨日、Endが明日
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetCalendarEventインスタンス
        //   - 日付を含むarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockCalendarEventRepository = new MockCalendarEventRepository();

        var today = DateTime.Today;
        var eventItem = new Google.Apis.Calendar.v3.Data.Event
        {
            Summary = "複数日イベント",
            Start = new Google.Apis.Calendar.v3.Data.EventDateTime
            {
                DateTimeDateTimeOffset = new DateTimeOffset(today.AddDays(-1).AddHours(10))
            },
            End = new Google.Apis.Calendar.v3.Data.EventDateTime
            {
                DateTimeDateTimeOffset = new DateTimeOffset(today.AddDays(1).AddHours(10))
            }
        };

        mockCalendarEventRepository.GetEventsAsyncFunc = (date) =>
            Task.FromResult<IList<Google.Apis.Calendar.v3.Data.Event>>(new List<Google.Apis.Calendar.v3.Data.Event> { eventItem });

        var mockLogger = new MockLogger();
        var getCalendarEvent = new GetCalendarEvent(mockPromptRepository, mockCalendarEventRepository, mockLogger);

        var arguments = new Dictionary<string, object>
        {
            { "date", today }
        };
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getCalendarEvent.Invoke(arguments, state);

        // Assert:
        //   - resultに時刻の表示が含まれない
        //   - resultにイベントのSummaryが含まれる
        result.Should().Contain("複数日イベント");
        result.Should().NotContain("から");
        result.Should().NotContain("まで");
        result.Should().Contain("- 複数日イベント");
    }

    /// <summary>
    /// テストの目的: Invokeが今日から始まり翌日に終わる予定を正しく処理すること
    /// テストする内容:
    /// - Startが今日、Endが今日でない
    /// - "から"のみ表示される
    /// 期待される動作: "- [HH:mmから] サマリー" 形式で返される
    /// </summary>
    [Fact]
    public async Task Invoke_今日から始まる予定_から表示されること()
    {
        // Arrange:
        //   - MockCalendarEventRepository
        //   - MockDateTimeProvider
        //   - イベントのStartが今日、Endが明日
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetCalendarEventインスタンス
        //   - 日付を含むarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockCalendarEventRepository = new MockCalendarEventRepository();

        var today = DateTime.Today;
        var eventItem = new Google.Apis.Calendar.v3.Data.Event
        {
            Summary = "今日から始まるイベント",
            Start = new Google.Apis.Calendar.v3.Data.EventDateTime
            {
                DateTimeDateTimeOffset = new DateTimeOffset(today.AddHours(18))
            },
            End = new Google.Apis.Calendar.v3.Data.EventDateTime
            {
                DateTimeDateTimeOffset = new DateTimeOffset(today.AddDays(1).AddHours(2))
            }
        };

        mockCalendarEventRepository.GetEventsAsyncFunc = (date) =>
            Task.FromResult<IList<Google.Apis.Calendar.v3.Data.Event>>(new List<Google.Apis.Calendar.v3.Data.Event> { eventItem });

        var mockLogger = new MockLogger();
        var getCalendarEvent = new GetCalendarEvent(mockPromptRepository, mockCalendarEventRepository, mockLogger);

        var arguments = new Dictionary<string, object>
        {
            { "date", today }
        };
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getCalendarEvent.Invoke(arguments, state);

        // Assert:
        //   - resultに"[HH:mmから]"が含まれる
        //   - resultに"まで"が含まれない
        //   - resultにイベントのSummaryが含まれる
        result.Should().Contain("[18:00から]");
        result.Should().NotContain("まで");
        result.Should().Contain("今日から始まるイベント");
    }

    /// <summary>
    /// テストの目的: Invokeが昨日から始まり今日で終わる予定を正しく処理すること
    /// テストする内容:
    /// - Startが今日でない、Endが今日
    /// - "まで"のみ表示される
    /// 期待される動作: "- [HH:mmまで] サマリー" 形式で返される
    /// </summary>
    [Fact]
    public async Task Invoke_今日で終わる予定_まで表示されること()
    {
        // Arrange:
        //   - MockCalendarEventRepository
        //   - MockDateTimeProvider
        //   - イベントのStartが昨日、Endが今日
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetCalendarEventインスタンス
        //   - 日付を含むarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockCalendarEventRepository = new MockCalendarEventRepository();

        var today = DateTime.Today;
        var eventItem = new Google.Apis.Calendar.v3.Data.Event
        {
            Summary = "今日で終わるイベント",
            Start = new Google.Apis.Calendar.v3.Data.EventDateTime
            {
                DateTimeDateTimeOffset = new DateTimeOffset(today.AddDays(-1).AddHours(22))
            },
            End = new Google.Apis.Calendar.v3.Data.EventDateTime
            {
                DateTimeDateTimeOffset = new DateTimeOffset(today.AddHours(10))
            }
        };

        mockCalendarEventRepository.GetEventsAsyncFunc = (date) =>
            Task.FromResult<IList<Google.Apis.Calendar.v3.Data.Event>>(new List<Google.Apis.Calendar.v3.Data.Event> { eventItem });

        var mockLogger = new MockLogger();
        var getCalendarEvent = new GetCalendarEvent(mockPromptRepository, mockCalendarEventRepository, mockLogger);

        var arguments = new Dictionary<string, object>
        {
            { "date", today }
        };
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getCalendarEvent.Invoke(arguments, state);

        // Assert:
        //   - resultに"[HH:mmまで]"が含まれる
        //   - resultに"から"が含まれない
        //   - resultにイベントのSummaryが含まれる
        result.Should().Contain("[10:00まで]");
        result.Should().NotContain("から");
        result.Should().Contain("今日で終わるイベント");
    }

    /// <summary>
    /// テストの目的: Invokeが開始時刻と終了時刻が同じ予定を正しく処理すること
    /// テストする内容:
    /// - Start == End の場合
    /// - 時刻のみ表示される
    /// 期待される動作: "- [HH:mm] サマリー" 形式で返される
    /// </summary>
    [Fact]
    public async Task Invoke_開始終了が同じ予定_時刻のみ表示されること()
    {
        // Arrange:
        //   - MockCalendarEventRepository
        //   - イベントのStartとEndが同じ時刻
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetCalendarEventインスタンス
        //   - 日付を含むarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockCalendarEventRepository = new MockCalendarEventRepository();

        var today = DateTime.Today;
        var sameTime = new DateTimeOffset(today.AddHours(14));
        var eventItem = new Google.Apis.Calendar.v3.Data.Event
        {
            Summary = "瞬時イベント",
            Start = new Google.Apis.Calendar.v3.Data.EventDateTime
            {
                DateTimeDateTimeOffset = sameTime
            },
            End = new Google.Apis.Calendar.v3.Data.EventDateTime
            {
                DateTimeDateTimeOffset = sameTime
            }
        };

        mockCalendarEventRepository.GetEventsAsyncFunc = (date) =>
            Task.FromResult<IList<Google.Apis.Calendar.v3.Data.Event>>(new List<Google.Apis.Calendar.v3.Data.Event> { eventItem });

        var mockLogger = new MockLogger();
        var getCalendarEvent = new GetCalendarEvent(mockPromptRepository, mockCalendarEventRepository, mockLogger);

        var arguments = new Dictionary<string, object>
        {
            { "date", today }
        };
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getCalendarEvent.Invoke(arguments, state);

        // Assert:
        //   - resultに"[HH:mm]"が含まれる
        //   - resultに"から"も"まで"も含まれない
        //   - resultにイベントのSummaryが含まれる
        result.Should().Contain("[14:00]");
        result.Should().NotContain("から");
        result.Should().NotContain("まで");
        result.Should().Contain("瞬時イベント");
    }

    #endregion

    #region Invoke テスト - 異常系

    /// <summary>
    /// テストの目的: Invokeが予定取得時に例外が発生した場合にエラーメッセージを返すこと
    /// テストする内容:
    /// - GetEventsAsyncが例外をスローする
    /// - 例外がキャッチされる
    /// - エラーメッセージが返される
    /// - Loggerにエラーが記録される
    /// 期待される動作: "予定が取得できませんでした"というエラーメッセージが返される
    /// </summary>
    [Fact]
    public async Task Invoke_予定取得時に例外発生_エラーメッセージを返すこと()
    {
        // Arrange:
        //   - MockCalendarEventRepository (GetEventsAsyncで例外をスロー)
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetCalendarEventインスタンス
        //   - 日付を含むarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockCalendarEventRepository = new MockCalendarEventRepository();
        mockCalendarEventRepository.GetEventsAsyncFunc = (date) =>
            throw new Exception("Test exception");

        var mockLogger = new MockLogger();
        var getCalendarEvent = new GetCalendarEvent(mockPromptRepository, mockCalendarEventRepository, mockLogger);

        var arguments = new Dictionary<string, object>
        {
            { "date", new DateTime(2025, 1, 15) }
        };
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getCalendarEvent.Invoke(arguments, state);

        // Assert:
        //   - result == "予定が取得できませんでした"
        //   - Logger.LogErrorが1回呼ばれる
        //   - 例外が外部に伝播しない
        result.Should().Be("予定が取得できませんでした");
        mockLogger.Errors.Should().HaveCount(1);
        mockLogger.Errors[0].Message.Should().Be("Test exception");
    }

    /// <summary>
    /// テストの目的: Invokeが異なる種類の例外も正しく処理できること
    /// テストする内容:
    /// - InvalidOperationExceptionが発生した場合
    /// - TimeoutExceptionが発生した場合
    /// - すべての例外が適切にキャッチされる
    /// 期待される動作: どの例外でも同じエラーメッセージが返される
    /// </summary>
    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(Exception))]
    public async Task Invoke_異なる種類の例外_すべて適切に処理されること(Type exceptionType)
    {
        // Arrange:
        //   - MockCalendarEventRepository (指定された型の例外をスロー)
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetCalendarEventインスタンス
        //   - 日付を含むarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockCalendarEventRepository = new MockCalendarEventRepository();

        var exception = (Exception)Activator.CreateInstance(exceptionType, "Test exception")!;
        mockCalendarEventRepository.GetEventsAsyncFunc = (date) => throw exception;

        var mockLogger = new MockLogger();
        var getCalendarEvent = new GetCalendarEvent(mockPromptRepository, mockCalendarEventRepository, mockLogger);

        var arguments = new Dictionary<string, object>
        {
            { "date", new DateTime(2025, 1, 15) }
        };
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getCalendarEvent.Invoke(arguments, state);

        // Assert:
        //   - result == "予定が取得できませんでした"
        //   - Logger.LogErrorが1回呼ばれる
        //   - 記録された例外の型が期待される型と一致する
        result.Should().Be("予定が取得できませんでした");
        mockLogger.Errors.Should().HaveCount(1);
        mockLogger.Errors[0].Should().BeOfType(exceptionType);
    }

    /// <summary>
    /// テストの目的: Invokeがargumentsにdateキーが存在しない場合の動作を確認
    /// テストする内容:
    /// - argumentsに"date"キーが含まれていない場合
    /// - KeyNotFoundExceptionまたは適切な例外がスローされる
    /// 期待される動作: 例外がキャッチされ、エラーメッセージが返される
    /// </summary>
    [Fact]
    public async Task Invoke_argumentsにdateなし_例外が処理されること()
    {
        // Arrange:
        //   - MockCalendarEventRepository
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetCalendarEventインスタンス
        //   - 空のarguments辞書
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockCalendarEventRepository = new MockCalendarEventRepository();
        var mockLogger = new MockLogger();
        var getCalendarEvent = new GetCalendarEvent(mockPromptRepository, mockCalendarEventRepository, mockLogger);

        var arguments = new Dictionary<string, object>();  // 空の辞書
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getCalendarEvent.Invoke(arguments, state);

        // Assert:
        //   - result == "予定が取得できませんでした"
        //   - Logger.LogErrorが1回呼ばれる
        result.Should().Be("予定が取得できませんでした");
        mockLogger.Errors.Should().HaveCount(1);
    }

    #endregion

    #region Description テスト

    /// <summary>
    /// テストの目的: DescriptionプロパティがPromptRepositoryから取得した値を返すこと
    /// テストする内容:
    /// - PromptRepository.GetCalendarEventDescriptionが正しく返される
    /// 期待される動作: Descriptionプロパティがプロンプトリポジトリの値と一致する
    /// </summary>
    [Fact]
    public void Description_PromptRepositoryの値を返すこと()
    {
        // Arrange: MockPromptRepositoryとGetCalendarEventインスタンスを準備
        var mockPromptRepository = new MockPromptRepository();
        var mockCalendarEventRepository = new MockCalendarEventRepository();
        var mockLogger = new MockLogger();
        var getCalendarEvent = new GetCalendarEvent(mockPromptRepository, mockCalendarEventRepository, mockLogger);

        // Act: Descriptionプロパティを取得
        var description = getCalendarEvent.Description;

        // Assert: Description == MockPromptRepository.GetCalendarEventDescription
        description.Should().Be(mockPromptRepository.GetCalendarEventDescription);
    }

    #endregion

    #region Parameters テスト

    /// <summary>
    /// テストの目的: Parametersプロパティが正しいJSONスキーマを返すこと
    /// テストする内容:
    /// - Parametersプロパティが正しいJSON形式を返す
    /// - typeがobjectであること
    /// - propertiesにdateプロパティが存在すること
    /// - dateのtypeがstringであること
    /// - requiredに"date"が含まれること
    /// - additionalPropertiesがfalseであること
    /// 期待される動作: 正しいパラメータスキーマを表す有効なJSONが返される
    /// </summary>
    [Fact]
    public void Parameters_正しいJSONスキーマを返すこと()
    {
        // Arrange: GetCalendarEventインスタンスを準備
        var mockPromptRepository = new MockPromptRepository();
        var mockCalendarEventRepository = new MockCalendarEventRepository();
        var mockLogger = new MockLogger();
        var getCalendarEvent = new GetCalendarEvent(mockPromptRepository, mockCalendarEventRepository, mockLogger);

        // Act: Parametersプロパティを取得
        var parameters = getCalendarEvent.Parameters;

        // Assert:
        //   - Parametersが有効なJSONである
        //   - パースしたJSONが期待される構造を持つ
        //   - properties.date.type == "string"
        //   - properties.date.description が存在する
        //   - required に "date" が含まれる
        //   - additionalProperties == false
        using var doc = JsonDocument.Parse(parameters);
        var root = doc.RootElement;

        root.GetProperty("type").GetString().Should().Be("object");

        var properties = root.GetProperty("properties");
        properties.TryGetProperty("date", out var dateProperty).Should().BeTrue();
        dateProperty.GetProperty("type").GetString().Should().Be("string");
        dateProperty.TryGetProperty("description", out _).Should().BeTrue();

        var required = root.GetProperty("required");
        var requiredItems = required.EnumerateArray().Select(e => e.GetString()).ToList();
        requiredItems.Should().Contain("date");

        root.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
    }

    #endregion
}
