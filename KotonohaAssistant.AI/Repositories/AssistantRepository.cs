using KotonohaAssistant.AI.Functions;
using OpenAI;
using OpenAI.Assistants;
using System.ClientModel;

namespace KotonohaAssistant.AI.Repositories;

public interface IAssistantRepository
{
    Task<Assistant> RegisterAssistantAsync(string model, string assistantName, string instructions, IEnumerable<ToolFunction> functions);
    IAsyncEnumerable<Assistant> GetAssistantsAsync();
    Task<AssistantThread> CreateThreadAsync(IList<(MessageRole role, string content)> initialMessages);
    Task<ThreadRun> CreateRunAsync(string threadId, string assistantId);
    Task<ThreadRun> WaitForRunCompletedAsync(string threadId, string runId);
    Task<ThreadRun> SubmitFunctionOutputAsync(string threadId, string runId, string toolCallId, string value);
    Task<ThreadMessage> CreateUserMessageAsync(string threadId, string content);
    Task<ThreadMessage?> GetLatestMessageAsync(string threadId);
    Task<bool> DeleteMessageAsync(string threadId, string messageId);
}

public class AssistantRepository(string apiKey) : IAssistantRepository
{
    private readonly OpenAIClient _client = new(apiKey);
    private AssistantClient Client => _client.GetAssistantClient();

    public async Task<Assistant> RegisterAssistantAsync(string model, string assistantName, string instructions, IEnumerable<ToolFunction> functions)
    {
        var options = new AssistantCreationOptions
        {
            Name = assistantName,
            Instructions = instructions,
        };
        foreach (var function in functions)
        {
            options.Tools.Add(function.CreateDefinition());
        }

        var assistant = await Client.CreateAssistantAsync(model, options);

        return assistant.Value;
    }

    public async IAsyncEnumerable<Assistant> GetAssistantsAsync()
    {
        await foreach (var assistant in Client.GetAssistantsAsync())
        {
            yield return assistant;
        }
    }

    public async Task<AssistantThread> CreateThreadAsync(IList<(MessageRole role, string content)> initialMessages)
    {
        var options = new ThreadCreationOptions();
        foreach (var (role, content) in initialMessages)
        {
            options.InitialMessages.Add(new ThreadInitializationMessage(role, [content]));
        }

        var thread = await Client.CreateThreadAsync(options);

        return thread.Value;
    }

    public async Task<ThreadRun> CreateRunAsync(string threadId, string assistantId)
    {
        var run = await Client.CreateRunAsync(threadId, assistantId);

        return await WaitForRunCompletedAsync(threadId, run.Value.Id);
    }

    public async Task<ThreadRun> WaitForRunCompletedAsync(string threadId, string runId)
    {
        ClientResult<ThreadRun>? run;
        do
        {
            await Task.Delay(500);
            run = await Client.GetRunAsync(threadId, runId);
        }
        while (run.Value.Status == RunStatus.Queued || run.Value.Status == RunStatus.InProgress);

        return run.Value;
    }

    public async Task<ThreadRun> SubmitFunctionOutputAsync(string threadId, string runId, string toolCallId, string value)
    {
        return await Client.SubmitToolOutputsToRunAsync(threadId, runId, [
            new ToolOutput{
                ToolCallId = toolCallId,
                Output = value,
            }]);
    }

    public async Task<ThreadMessage> CreateUserMessageAsync(string threadId, string content)
    {
        var message = await Client.CreateMessageAsync(
            threadId,
            MessageRole.User,
            [MessageContent.FromText(content)]);

        return message.Value;
    }

    public async Task<ThreadMessage?> GetLatestMessageAsync(string threadId)
    {
        await using var enumerator = Client.GetMessagesAsync(threadId).GetAsyncEnumerator();

        if (await enumerator.MoveNextAsync())
        {
            return enumerator.Current;
        }

        return null;
    }

    public async Task<bool> DeleteMessageAsync(string threadId, string messageId)
    {
        var result = await Client.DeleteMessageAsync(threadId, messageId);

        return result.Value.Deleted;
    }
}
