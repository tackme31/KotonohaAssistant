using System.Text;
using System.Text.Json;
using KotonohaAssistant.AI.Utils;
using KotonohaAssistant.Core.Utils;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Functions;

public abstract class ToolFunction(ILogger logger)
{
    public abstract string Description { get; }
    public abstract string Parameters { get; }

    protected ILogger Logger { get; } = logger;

    /// <summary>
    /// ë”ÇØï»ëŒè€Ç©Ç«Ç§Ç©
    /// </summary>
    public virtual bool CanBeLazy { get; set; } = true;

    public abstract bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments);

    public abstract Task<string> Invoke(IDictionary<string, object> arguments, IReadOnlyConversationState state);

    public ChatTool CreateChatTool()
    {
        return ChatTool.CreateFunctionTool(
          functionName: GetType().Name,
          functionDescription: Description,
          functionParameters: BinaryData.FromBytes(Encoding.UTF8.GetBytes(Parameters))
        );
    }
}