using System.Text;
using System.Text.Json;
using OpenAI.Chat;

namespace KotonohaAssistant.Functions;

abstract class ToolFunction
{
  public abstract string Description { get; }
  public abstract string Parameters { get; }

  public abstract string Invoke(JsonDocument arguments);

  public ChatTool CreateChatTool()
  {
    return ChatTool.CreateFunctionTool(
      functionName: GetType().Name,
      functionDescription: Description,
      functionParameters: System.BinaryData.FromBytes(Encoding.UTF8.GetBytes(Parameters))
    );
  }
}