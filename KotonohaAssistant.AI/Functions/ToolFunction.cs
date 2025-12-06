using System.Text;
using System.Text.Json;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Functions;

public abstract class ToolFunction(ILogger logger)
{

    protected ILogger Logger { get; } = logger;

    /// <summary>
    /// 関数の説明
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// 関数のパラメータ
    /// </summary>
    public abstract string Parameters { get; }

    /// <summary>
    /// 怠け癖対象かどうか
    /// </summary>
    public virtual bool CanBeLazy { get; set; } = true;

    /// <summary>
    /// 引数のパース処理
    /// </summary>
    /// <param name="doc"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    public abstract bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments);

    /// <summary>
    /// 関数の実行処理
    /// </summary>
    /// <param name="arguments"></param>
    /// <param name="state"></param>
    /// <returns></returns>
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
