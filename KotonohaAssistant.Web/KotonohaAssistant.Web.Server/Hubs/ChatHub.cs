using KotonohaAssistant.AI.Services;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace KotonohaAssistant.Web.Server.Hubs;

public class ChatHub : Hub
{
    private static readonly ConcurrentDictionary<string, ConversationService> _services = new();

    public async Task SendMessage(string message)
    {
        var service = _services.GetOrAdd(Context.ConnectionId, CreateConversationService);

        await foreach (var text in service.TalkingWithKotonohaSisters(message))
        {
            await Clients.Caller.SendAsync("Generated", text);
        }

        await Clients.Caller.SendAsync("Complete", "COMPLETED");
    }

    private ConversationService CreateConversationService(string connectionId)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (apiKey == null)
        {
            throw new Exception("環境変数 'OPENAI_API_KEY' が設定されていません。環境変数に設定するか、または '.env' ファイルにAPIキーを記載してください。");
        }

        var service = new ConversationService(
            apiKey,
            Const.ModelName,
            Const.Functions,
            Const.ExcludeFunctionNamesFromLazyMode);

        return service;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (_services.TryRemove(Context.ConnectionId, out var service))
        {
            service.Dispose();
        }

        return base.OnDisconnectedAsync(exception);
    }
}