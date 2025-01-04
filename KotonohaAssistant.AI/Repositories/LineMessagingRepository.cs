using KotonohaAssistant.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KotonohaAssistant.AI.Repositories;

public interface ILineMessagingRepository
{
}

public class LineMessagingRepository(string accessToken, string userId) : ILineMessagingRepository
{
    private readonly string _accessToken = accessToken;
    private readonly string _userId = userId;

    public async Task SendMessage(string message)
    {
        // LINE Messaging APIでメッセージ送信

        // 採集会話日時から3以上経過すると、寂しそうなメッセージをLINEに送信する
    }
}
