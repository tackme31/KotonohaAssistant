using KotonohaAssistant.Core.Utils;
using Line.Messaging;

namespace KotonohaAssistant.AI.Repositories;

/// <summary>
/// LINE Messaging APIとの通信を担当するRepository
/// </summary>
public interface ILineMessagingRepository
{
    /// <summary>
    /// 指定されたユーザーにテキストメッセージを送信
    /// </summary>
    /// <param name="userId">送信先のLINEユーザーID</param>
    /// <param name="message">送信するテキストメッセージ</param>
    Task SendTextMessageAsync(string userId, string message);
}

/// <summary>
/// LINE Messaging APIを使用した実装
/// </summary>
public class LineMessagingRepository : ILineMessagingRepository
{
    private readonly LineMessagingClient _client;
    private readonly ILogger _logger;

    public LineMessagingRepository(string channelAccessToken, ILogger logger)
    {
        _client = new LineMessagingClient(channelAccessToken);
        _logger = logger;
    }

    public async Task SendTextMessageAsync(string userId, string message)
    {
        try
        {
            var messages = new List<ISendMessage>
            {
                new TextMessage(message)
            };
            await _client.PushMessageAsync(userId, messages);
            _logger.LogInformation($"[LINE] Message sent successfully to user: {userId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
            throw;
        }
    }
}

/// <summary>
/// LINE未設定時のNull Object Pattern実装
/// </summary>
public class NullLineMessagingRepository : ILineMessagingRepository
{
    private readonly ILogger _logger;

    public NullLineMessagingRepository(ILogger logger)
    {
        _logger = logger;
    }

    public Task SendTextMessageAsync(string userId, string message)
    {
        _logger.LogWarning("[LINE] LINE notification is not configured. Skipping notification.");
        return Task.CompletedTask;
    }
}
