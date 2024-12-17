using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Services;
using Microsoft.AspNetCore.SignalR;

namespace KotonohaAssistant.Web.Server.Hubs;

public class ChatHub : Hub
{
    private static readonly List<ToolFunction> Functions =
    [
        new SetAlarm(),
        new StartTimer(),
        new CreateCalendarEvent(),
        new GetCalendarEvent(),
        new GetWeather(),
        new TurnOnHeater(),
        new ForgetMemory(),
    ];

    // 怠け癖の対象外の関数
    private static readonly List<string> ExcludeFunctionNamesFromLazyMode =
    [
        nameof(StartTimer),
        nameof(ForgetMemory)
    ];

    private static ConversationService? ConversationService;

    // クライアントからメッセージを受け取る
    public async Task SendMessage(string message)
    {
        if (ConversationService is null)
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var modelName = "gpt-4o-mini";
            ConversationService = new ConversationService(apiKey, modelName, Functions, ExcludeFunctionNamesFromLazyMode);
        }

        await foreach (var text in ConversationService.SpeakAI(message))
        {
            await Clients.Caller.SendAsync("MessageGenerated", text);
        }

        await Clients.Caller.SendAsync("Complete", "読み上げが完了しました。");
    }
}