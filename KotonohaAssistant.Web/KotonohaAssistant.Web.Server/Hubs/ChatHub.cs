using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Services;
using Microsoft.AspNetCore.SignalR;

namespace KotonohaAssistant.Web.Server.Hubs;

public class ChatHub : Hub
{
    private static readonly List<ToolFunction> Functions =
    [
/*        new SetAlarm(),
        new StartTimer(),
        new CreateCalendarEvent(),
        new GetCalendarEvent(),
        new GetWeather(),
        new TurnOnHeater(),
        new ForgetMemory(),*/
    ];

    // 怠け癖の対象外の関数
    private static readonly List<string> ExcludeFunctionNamesFromLazyMode =
    [
/*        nameof(StartTimer),
        nameof(ForgetMemory)*/
    ];

    private static ConversationService? _conversationService;

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
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new Exception("環境変数 'OPENAI_API_KEY' が設定されていません。環境変数に設定するか、または '.env' ファイルにAPIキーを記載してください。");
        var modelName = "gpt-4o-mini";
        _conversationService = new ConversationService(apiKey, modelName, Functions, ExcludeFunctionNamesFromLazyMode);

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