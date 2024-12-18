using System.Text;
using System.Text.Json;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Functions;

public abstract class ToolFunction
{
    public abstract string Description { get; }
    public abstract string Parameters { get; }

    public abstract bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments);

    public abstract string Invoke(IDictionary<string, object> arguments);

    public ChatTool CreateChatTool()
    {
        return ChatTool.CreateFunctionTool(
          functionName: GetType().Name,
          functionDescription: Description,
          functionParameters: BinaryData.FromBytes(Encoding.UTF8.GetBytes(Parameters))
        );
    }
}