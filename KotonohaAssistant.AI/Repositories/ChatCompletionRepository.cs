using OpenAI.Chat;
using System.ClientModel;

namespace KotonohaAssistant.AI.Repositories;

public interface IChatCompletionRepository
{
    Task<ClientResult<ChatCompletion>> CompleteChatAsync(IEnumerable<ChatMessage> messages);
}

public class ChatCompletionRepository(string modelName, string apiKey, ChatCompletionOptions options) : IChatCompletionRepository
{
    private readonly ChatCompletionOptions _options = options;
    private readonly ChatClient _client = new(modelName, apiKey);

    public async Task<ClientResult<ChatCompletion>> CompleteChatAsync(IEnumerable<ChatMessage> messages)
    {
        return await _client.CompleteChatAsync(messages, _options);
    }
}
