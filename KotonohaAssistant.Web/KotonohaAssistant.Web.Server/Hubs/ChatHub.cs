using Microsoft.AspNetCore.SignalR;

namespace KotonohaAssistant.Web.Server.Hubs;

public class ChatHub : Hub
{
    // クライアントからメッセージを受け取る
    public async Task SendMessage(string message)
    {
        // 1. "Hello, world" を即座に返す
        await Clients.Caller.SendAsync("MessageGenerated", "Hello, you said: " + message);

        // 2. 5秒後に「読み上げ完了」を通知
        //await Task.Delay(5000);
        //await Clients.Caller.SendAsync("ReadingComplete", "読み上げが完了しました。");
    }
}