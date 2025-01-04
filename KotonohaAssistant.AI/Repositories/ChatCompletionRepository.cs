using KotonohaAssistant.Core.Utils;
using OpenAI.Chat;
using System.ClientModel;

namespace KotonohaAssistant.AI.Repositories;

public interface IChatCompletionRepository
{
    Task<ClientResult<ChatCompletion>> CompleteChatAsync(IEnumerable<ChatMessage> messages, ChatCompletionOptions? options = null);
}

public class ChatCompletionRepository(string modelName, string apiKey) : IChatCompletionRepository
{
    private readonly ChatClient _client = new(modelName, apiKey);

    public async Task<ClientResult<ChatCompletion>> CompleteChatAsync(IEnumerable<ChatMessage> messages, ChatCompletionOptions? options = null)
    {
        return await _client.CompleteChatAsync(messages, options);
    }
}
