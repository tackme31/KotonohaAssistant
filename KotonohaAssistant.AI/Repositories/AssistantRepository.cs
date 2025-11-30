using KotonohaAssistant.AI.Functions;
using OpenAI;
using OpenAI.Assistants;
using System.ClientModel;

namespace KotonohaAssistant.AI.Repositories;

public interface IAssistantRepository
{
    Task<Assistant> CreateAssistantAsync(string model, string assistantName, string instructions, IEnumerable<ToolFunction> functions);
    IAsyncEnumerable<Assistant> GetAssistantsAsync();
    Task<Assistant> GetAssistantAsync(string id);
    Task<AssistantThread> CreateThreadAsync(IList<(MessageRole role, string content)> initialMessages);
    Task<ThreadRun> CreateRunAsync(string threadId, string assistantId);
    Task<ThreadRun> CancelRunAsync(string threadId, string runId);
    Task<ThreadRun> WaitForRunCompletedAsync(string threadId, string runId);
    Task<ThreadRun> SubmitFunctionOutputsAsync(string threadId, string runId, IEnumerable<(string toolCallId, string value)> outputs)
    Task<ThreadMessage> CreateMessageAsync(string threadId, MessageRole role, params string[] contents);
    Task<List<ThreadMessage>> GetMessagesAsync(string threadId);
    Task<ThreadMessage?> GetLatestMessageAsync(string threadId);
    Task<bool> DeleteMessageAsync(string threadId, string messageId);
}

public class AssistantRepository(string apiKey) : IAssistantRepository
{
    private readonly OpenAIClient _client = new(apiKey);
    private AssistantClient Client => _client.GetAssistantClient();

    public async Task<Assistant> CreateAssistantAsync(string model, string assistantName, string instructions, IEnumerable<ToolFunction> functions)
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

    public async Task<Assistant> GetAssistantAsync(string id)
    {
        var assistant = await Client.GetAssistantAsync(id);
        return assistant.Value;
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
        return run.Value;
    }

    public async Task<ThreadRun> CancelRunAsync(string threadId, string runId)
    {
        var run = await Client.CancelRunAsync(threadId, runId);
        return run.Value;
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

    public async Task<ThreadRun> SubmitFunctionOutputsAsync(string threadId, string runId, IEnumerable<(string toolCallId, string value)> outputs)
    {
        return await Client.SubmitToolOutputsToRunAsync(
            threadId,
            runId,
            outputs.Select(output => new ToolOutput
            {
                ToolCallId = output.toolCallId,
                Output = output.value
            }));
    }

    public async Task<ThreadMessage> CreateMessageAsync(string threadId, MessageRole role, params string[] contents)
    {
        var message = await Client.CreateMessageAsync(
            threadId,
            role,
            [.. contents.Select(content => MessageContent.FromText(content))]);

        return message.Value;
    }

    public async Task<List<ThreadMessage>> GetMessagesAsync(string threadId)
    {
        var messages = new List<ThreadMessage>();
        await foreach (var message in Client.GetMessagesAsync(threadId))
        {
            messages.Add(message);
        }

        return messages;
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
