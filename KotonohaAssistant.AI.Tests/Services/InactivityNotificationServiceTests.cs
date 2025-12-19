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

public class InactivityNotificationServiceTests
{
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
        private readonly List<Message> _messages = new();
        private long _conversationId = 1;

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
        public override System.IO.Stream? ContentStream { get; set; }
        public override System.BinaryData Content => BinaryData.FromString("{}");

        protected override System.ClientModel.Primitives.PipelineResponseHeaders HeadersCore => new MockPipelineResponseHeaders();

        public override System.BinaryData BufferContent(CancellationToken cancellationToken = default)
        {
            return Content;
        }

        public override async ValueTask<System.BinaryData> BufferContentAsync(CancellationToken cancellationToken = default)
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
        public List<string> InformationLogs { get; } = new();
        public List<string> WarningLogs { get; } = new();
        public List<Exception> Errors { get; } = new();

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
        public List<(string userId, string message)> SentMessages { get; } = new();

        public Task SendTextMessageAsync(string userId, string message)
        {
            SentMessages.Add((userId, message));
            return Task.CompletedTask;
        }
    }

    #endregion

    #region Start メソッドのスケジューリングテスト

    [Fact]
    public void Start_notifyTimeが現在時刻より前の場合_翌日にスケジュールされること()
    {
        // テスト内容:
        // - 現在時刻が 12:00 の場合
        // - notifyTime が 09:00 (現在時刻より前) の場合
        // - 翌日の 09:00 にタイマーがスケジュールされること
        //
        // 期待される値:
        // - ログに「Next check scheduled at: {翌日の09:00}」が出力される
        // - タイマーの遅延時間が (24 - 3) * 60 * 60 = 75600 秒であること

        throw new NotImplementedException();
    }

    [Fact]
    public void Start_notifyTimeが現在時刻より後の場合_当日にスケジュールされること()
    {
        // テスト内容:
        // - 現在時刻が 12:00 の場合
        // - notifyTime が 15:00 (現在時刻より後) の場合
        // - 当日の 15:00 にタイマーがスケジュールされること
        //
        // 期待される値:
        // - ログに「Next check scheduled at: {当日の15:00}」が出力される
        // - タイマーの遅延時間が 3 * 60 * 60 = 10800 秒であること

        throw new NotImplementedException();
    }

    [Fact]
    public void Start_複数回呼び出し_既存のタイマーが破棄されること()
    {
        // テスト内容:
        // - Start メソッドを 2 回呼び出す
        // - 1 回目のタイマーが破棄され、2 回目のタイマーが設定されること
        //
        // 期待される値:
        // - 例外が発生しないこと
        // - 最後に設定されたタイマーのみが有効であること

        throw new NotImplementedException();
    }

    #endregion

    #region CheckInactivityAsync テスト

    [Fact]
    public async Task CheckInactivityAsync_非アクティブ期間経過_LINE通知が送信されること()
    {
        // テスト内容:
        // - 最後のアクティビティが 2 時間前
        // - notifyInterval が 1 時間の場合
        // - LINE 通知が送信されること
        //
        // 期待される値:
        // - LineMessagingRepository.SendTextMessageAsync が 1 回呼ばれる
        // - ログに「Sending inactivity reminder...」が出力される
        // - 送信されたメッセージが空でないこと

        throw new NotImplementedException();
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
