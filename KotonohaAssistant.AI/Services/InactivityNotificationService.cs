using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Models;
using KotonohaAssistant.Core.Utils;
using OpenAI.Chat;
using System;

namespace KotonohaAssistant.AI.Services;

public interface IInactivityNotificationService
{
    void Start(TimeSpan notifyInterval, TimeSpan notifyTime);
}

public class InactivityNotificationService : IInactivityNotificationService, IDisposable
{
    private const string LogPrefix = "[Inactivity]";

    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly IChatCompletionRepository _chatCompletionRepository;
    private readonly ChatCompletionOptions _options;
    private readonly IPromptRepository _promptRepository;
    private readonly ILogger _logger;
    private readonly ILineMessagingRepository _lineMessagingRepository;
    private readonly string _lineUserId;
    private Timer? _timer;

    public InactivityNotificationService(
        IChatMessageRepository chatMessageRepository,
        IChatCompletionRepository chatCompletionRepository,
        IList<ToolFunction> availableFunctions,
        IPromptRepository promptRepository,
        ILogger logger,
        ILineMessagingRepository lineMessagingRepository,
        string lineUserId)
    {
        _chatMessageRepository = chatMessageRepository;
        _chatCompletionRepository = chatCompletionRepository;
        _promptRepository = promptRepository;
        _logger = logger;
        _lineMessagingRepository = lineMessagingRepository;
        _lineUserId = lineUserId;

        _options = new();
        foreach (var func in availableFunctions)
        {
            _options.Tools.Add(func.CreateChatTool());
        }
    }

    public void Start(TimeSpan notifyInterval, TimeSpan notifyTime)
    {
        ScheduleNextRun(notifyInterval, notifyTime);
    }

    private void ScheduleNextRun(TimeSpan notifyInterval, TimeSpan notifyTime)
    {
        var now = DateTime.Now;
        var todayTarget = now.Date.Add(notifyTime);

        DateTime next;

        if (now < todayTarget)
        {
            next = todayTarget;
        }
        else
        {
            next = todayTarget.AddDays(1);
        }

        var delay = next - now;

        _logger.LogInformation($"{LogPrefix} Next check scheduled at: {next}");

        _timer?.Dispose();
        _timer = new Timer(async _ => await RunAndRescheduleAsync(notifyInterval, notifyTime), null, delay, Timeout.InfiniteTimeSpan);
    }

    private async Task RunAndRescheduleAsync(TimeSpan notifyInterval, TimeSpan notifyTime)
    {
        try
        {
            await CheckInactivityAsync(notifyInterval);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
        }
        finally
        {
            ScheduleNextRun(notifyInterval, notifyTime);
        }
    }

    private async Task CheckInactivityAsync(TimeSpan notifyInterval)
    {
        _logger.LogInformation($"{LogPrefix} Checking user inactivity...");

        // 最終アクセス日時チェック
        var conversationId = await _chatMessageRepository.GetLatestConversationIdAsync();
        var messages = await _chatMessageRepository.GetAllMessageAsync(conversationId);
        var lastActivity = GetLastActivity(messages);

        if (lastActivity is null)
        {
            _logger.LogWarning($"{LogPrefix} No activity found. Skipping.");
            return;
        }

        var (createdAt, sister, convId) = lastActivity.Value;
        var now = DateTime.Now;
        var elapsed = now - createdAt;

        _logger.LogInformation($"{LogPrefix} Last active: {createdAt}, elapsed: {elapsed}");

        // 非アクティブ間隔を超えていたら LINE 通知
        if (elapsed >= notifyInterval)
        {
            await SendInactivityNotificationAsync(notifyInterval, sister, convId);
        }
        else
        {
            _logger.LogInformation($"{LogPrefix} Not enough time elapsed. No notification.");
        }
    }

    private (DateTime? createdAt, Kotonoha sister, long conversationId)? GetLastActivity(IEnumerable<Message?> messages)
    {
        foreach (var m in messages.OfType<Message>().Reverse())
        {
            if (m.Content is not null &&
                ChatResponse.TryParse(m.Content, out var content) &&
                m.ConversationId.HasValue &&
                m.CreatedAt.HasValue)
            {
                return (m.CreatedAt.Value, content!.Assistant, m.ConversationId.Value);
            }
        }
        return null;
    }

    /// <summary>
    /// LINE などへの通知処理
    /// </summary>
    private async Task SendInactivityNotificationAsync(TimeSpan notifyInterval, Kotonoha sister, long conversationId)
    {
        var allChatMessages = await _chatMessageRepository.GetAllChatMessagesAsync(conversationId);
        var state = new ConversationState
        {
            CharacterPromptAkane = _promptRepository.GetCharacterPrompt(Kotonoha.Akane),
            CharacterPromptAoi = _promptRepository.GetCharacterPrompt(Kotonoha.Aoi),
            CurrentSister = sister,
        };

        state.LoadInitialConversation();
        state.LoadMessages(allChatMessages.OfType<ChatMessage>());
        state.AddInstruction(Instruction.SwitchSisterTo(sister));
        state.AddInstruction(_promptRepository.InactiveNotification(notifyInterval));

        string lineMessage;
        try
        {
            // 通知メッセージを作成
            var result = await _chatCompletionRepository.CompleteChatAsync(state.ChatMessagesWithSystemMessage, _options);
            var message = new AssistantChatMessage(result.Value);
            var content = result.Value.Content[0].Text;
            if (!ChatResponse.TryParse(content, out var res))
            {
                _logger.LogWarning($"{LogPrefix} The response couldn't be parsed to ChatResponse: {content}");
                return;
            }

            await _chatMessageRepository.InsertChatMessagesAsync([message], conversationId);

            lineMessage = res!.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
            return;
        }

        if (string.IsNullOrEmpty(lineMessage))
        {
            _logger.LogWarning($"{LogPrefix} Generated message is empty. Skipping notification.");
            return;
        }

        _logger.LogInformation($"{LogPrefix} Sending inactivity reminder...");

        await _lineMessagingRepository.SendTextMessageAsync(_lineUserId, lineMessage);
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
