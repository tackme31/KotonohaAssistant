using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Utils;
using Moq;

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

    private ConversationService CreateService(
        IList<ToolFunction>? availableFunctions = null,
        Kotonoha defaultSister = Kotonoha.Akane)
    {
        availableFunctions ??= new List<ToolFunction>();

        return new ConversationService(
            _mockPromptRepository.Object,
            _mockChatMessageRepository.Object,
            _mockChatCompletionRepository.Object,
            availableFunctions,
            _mockSisterSwitchingService.Object,
            _mockLazyModeHandler.Object,
            _mockLogger.Object,
            defaultSister
        );
    }

    // TODO: ここにテストメソッドを追加してください
}
