using KotonohaAssistant.AI.Services;
using Microsoft.AspNetCore.SignalR;

namespace KotonohaAssistant.Web.Server.Hubs;

public class ChatHub : Hub
{
    private ConversationService? _conversationService;

    // クライアントからメッセージを受け取る
    public async Task SendMessage(string message)
    {
        if (_conversationService is null)
        {
            return;
        }

        await foreach (var text in _conversationService.TalkingWithKotonohaSisters(message))
        {
            await Clients.Caller.SendAsync("Generated", text);
        }

        await Clients.Caller.SendAsync("Complete", "COMPLETED");
    }

    public override Task OnConnectedAsync()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (apiKey == null)
        {
            throw new Exception("環境変数 'OPENAI_API_KEY' が設定されていません。環境変数に設定するか、または '.env' ファイルにAPIキーを記載してください。");
        }

        _conversationService = new ConversationService(
            apiKey,
            Const.ModelName,
            Const.Functions,
            Const.ExcludeFunctionNamesFromLazyMode);

        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (_conversationService != null)
        {
            _conversationService.Dispose();
            _conversationService = null;
        }

        return base.OnDisconnectedAsync(exception);
    }
}