using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Utils;
using Moq;

namespace KotonohaAssistant.AI.Tests.Services;

public class InactivityNotificationServiceTests
{
    private readonly Mock<IChatMessageRepository> _mockChatMessageRepository;
    private readonly Mock<IChatCompletionRepository> _mockChatCompletionRepository;
    private readonly Mock<IPromptRepository> _mockPromptRepository;
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<ILineMessagingRepository> _mockLineMessagingRepository;

    public InactivityNotificationServiceTests()
    {
        _mockChatMessageRepository = new Mock<IChatMessageRepository>();
        _mockChatCompletionRepository = new Mock<IChatCompletionRepository>();
        _mockPromptRepository = new Mock<IPromptRepository>();
        _mockLogger = new Mock<ILogger>();
        _mockLineMessagingRepository = new Mock<ILineMessagingRepository>();
    }

    private InactivityNotificationService CreateService(
        IList<ToolFunction>? availableFunctions = null,
        string lineUserId = "test-user-id")
    {
        availableFunctions ??= new List<ToolFunction>();

        return new InactivityNotificationService(
            _mockChatMessageRepository.Object,
            _mockChatCompletionRepository.Object,
            availableFunctions,
            _mockPromptRepository.Object,
            _mockLogger.Object,
            _mockLineMessagingRepository.Object,
            lineUserId
        );
    }

    #region Start Tests

    [Fact]
    public void Start_ShouldScheduleNextRun()
    {
        // TODO: タイマーが正しくスケジュールされることを検証
        throw new NotImplementedException();
    }

    #endregion

    #region SendInactivityNotificationAsync Tests

    [Fact]
    public async Task SendInactivityNotificationAsync_ShouldLoadMessages()
    {
        // TODO: メッセージが読み込まれることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task SendInactivityNotificationAsync_ShouldGenerateNotificationMessage()
    {
        // TODO: 通知メッセージが生成されることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task SendInactivityNotificationAsync_ShouldSendLineMessage()
    {
        // TODO: LINEメッセージが送信されることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task SendInactivityNotificationAsync_WhenMessageIsEmpty_ShouldNotSend()
    {
        // TODO: メッセージが空の場合、送信しないことを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task SendInactivityNotificationAsync_WhenExceptionOccurs_ShouldLogError()
    {
        // TODO: 例外発生時にエラーがログされることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task SendInactivityNotificationAsync_WhenResponseNotParseable_ShouldLogWarning()
    {
        // TODO: 応答がパースできない場合、警告がログされることを検証
        throw new NotImplementedException();
    }

    #endregion

    #region CheckInactivityAsync Tests

    [Fact]
    public async Task CheckInactivityAsync_WhenNoActivity_ShouldSkip()
    {
        // TODO: アクティビティがない場合、スキップすることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task CheckInactivityAsync_WhenIntervalNotExceeded_ShouldNotSendNotification()
    {
        // TODO: 間隔を超えていない場合、通知を送信しないことを検証
        throw new NotImplementedException();
    }

    [Fact]
    public async Task CheckInactivityAsync_WhenIntervalExceeded_ShouldSendNotification()
    {
        // TODO: 間隔を超えた場合、通知を送信することを検証
        throw new NotImplementedException();
    }

    #endregion
}
