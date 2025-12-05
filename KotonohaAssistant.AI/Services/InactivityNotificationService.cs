using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Models;
using KotonohaAssistant.Core.Utils;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Services;

public interface IInactivityNotificationService
{
    void Start(TimeSpan notifyInterval, TimeSpan notifyTime);
}

public class InactivityNotificationService : IInactivityNotificationService
{
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly IChatCompletionRepository _chatCompletionRepository;
    private readonly IPromptRepository _promptRepository;
    private readonly ILogger _logger;
    private Timer? _timer;

    public InactivityNotificationService(
        IChatMessageRepository chatMessageRepository,
        IChatCompletionRepository chatCompletionRepository,
        IPromptRepository promptRepository,
        ILogger logger)
    {
        _chatMessageRepository = chatMessageRepository;
        _chatCompletionRepository = chatCompletionRepository;
        _promptRepository = promptRepository;
        _logger = logger;
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

        _logger.LogInformation($"[Inactivity] Next check scheduled at: {next}");

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
        _logger.LogInformation("[Inactivity] Checking user inactivity...");

        // 最終アクセス日時チェック
        var conversationId = await _chatMessageRepository.GetLatestConversationIdAsync();
        var messages = await _chatMessageRepository.GetAllMessageAsync(conversationId);
        var message = messages
            .OfType<Message>()
            .Select(m => new
            {
                m.ConversationId,
                m.CreatedAt,
                Content = m.Content is not null && ChatResponse.TryParse(m.Content, out var c) ? c : null
            })
            .LastOrDefault(m => m.Content is not null);

        if (message is null || message.Content is null || message.ConversationId is null)
        {
            _logger.LogWarning("[Inactivity] No activity found. Skipping.");
            return;
        }

        var now = DateTime.Now;
        var elapsed = now - message.CreatedAt;

        _logger.LogInformation($"[Inactivity] Last active: {message.CreatedAt}, elapsed: {elapsed}");

        // 非アクティブ間隔を超えていたら LINE 通知
        if (elapsed >= notifyInterval)
        {
            await SendInactivityNotificationAsync(notifyInterval, message.Content.Assistant, message.ConversationId.Value);
        }
        else
        {
            _logger.LogInformation("[Inactivity] Not enough time elapsed. No notification.");
        }
    }

    /// <summary>
    /// LINE などへの通知処理
    /// </summary>
    public async Task SendInactivityNotificationAsync(TimeSpan notifyInterval, Kotonoha sister, long conversationId)
    {
        // チャット補完
        // DBに追加
        // LINE通知
        var allChatMessages = await _chatMessageRepository.GetAllChatMessagesAsync(conversationId);
        var state = new ConversationState
        {
            CharacterPromptAkane = _promptRepository.GetCharacterPrompt(Kotonoha.Akane),
            CharacterPromptAoi = _promptRepository.GetCharacterPrompt(Kotonoha.Aoi),
            CurrentSister = sister,
        };

        foreach (var m in InitialConversation.Messages)
        {
            if (m.Sister is not null)
            {
                state.AddAssistantMessage(m.Sister.Value, m.Text, m.Emotion);
            }
            else
            {
                state.AddUserMessage(m.Text);
            }
        }

        state.LoadMessages(allChatMessages.OfType<ChatMessage>());
        state.AddInstruction(Instruction.SwitchSisterTo(sister));
        state.AddInstruction(Instruction.InactiveNotification(notifyInterval));

        string? lineMessage;
        try
        {
            var result = await _chatCompletionRepository.CompleteChatAsync(state.ChatMessagesWithSystemMessage);

            var message = new AssistantChatMessage(result.Value);
            var content = result.Value.Content[0].Text;
            if (!ChatResponse.TryParse(content, out var res))
            {
                _logger.LogWarning($"[Inactivity] The response couldn't be parsed to ChatResponse: {content}");
            }

            //await _chatMessageRepository.InsertChatMessagesAsync([message], conversationId);

            lineMessage = res!.Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
            return;
        }

        _logger.LogInformation("[Inactivity] Sending inactivity reminder...");
        await Task.CompletedTask;
    }
}
