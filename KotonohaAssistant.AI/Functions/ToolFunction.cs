using System.ComponentModel;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using System.Text.Unicode;
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
    /// パラメータの型
    /// </summary>
    protected abstract Type ParameterType { get; }

    /// <summary>
    /// 怠け癖対象かどうか
    /// </summary>
    public virtual bool CanBeLazy { get; set; } = true;

    /// <summary>
    /// 関数の実行処理
    /// </summary>
    /// <param name="argumentsDoc"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public abstract Task<string?> Invoke(JsonDocument argumentsDoc, IReadOnlyConversationState state);

    /// <summary>
    /// JsonDocumentをパースします
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="doc"></param>
    /// <returns></returns>
    protected T? Deserialize<T>(JsonDocument doc)
    {
        try
        {
            var result = doc.RootElement.Deserialize<T>(options: new()
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Disallow
            });

            // 追加のカスタムバリデーション
            if (result != null && !ValidateParameters(result))
            {
                Logger.LogWarning($"Parameter validation failed for {typeof(T).Name}");
                return default;
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            return default;
        }
    }

    /// <summary>
    /// パラメータの追加バリデーション（サブクラスでオーバーライド可能）
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="parameters">デシリアライズされたパラメータ</param>
    /// <returns>バリデーションが成功した場合true</returns>
    protected virtual bool ValidateParameters<T>(T parameters) => true;

    /// <summary>
    /// ツール定義を作成します
    /// </summary>
    /// <returns></returns>
    public ChatTool CreateChatTool()
    {
        var jsonSchema = JsonSchemaExporter.GetJsonSchemaAsNode(
                options: new()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
                },
                ParameterType,
                exporterOptions: new()
                {
                    TreatNullObliviousAsNonNullable = true,
                    TransformSchemaNode = (context, schema) =>
                    {
                        if (schema is not JsonObject newSchema)
                        {
                            return schema;
                        }

                        // ルートの処理
                        if (context.Path is [])
                        {
                            newSchema.Add("additionalProperties", false);
                            if (!newSchema.TryGetPropertyValue("properties", out _))
                            {
                                newSchema.Add("properties", new JsonObject());
                            }
                        }

                        // descriptionの処理
                        var attributeProvider = context.PropertyInfo?.AttributeProvider ?? context.TypeInfo.Type;
                        var descriptionAttr = attributeProvider?
                            .GetCustomAttributes(inherit: true)
                            .OfType<DescriptionAttribute>()
                            .FirstOrDefault();
                        if (descriptionAttr is not null)
                        {
                            newSchema.Insert(0, "description", descriptionAttr.Description);
                        }

                        return newSchema;
                    },
                });

        var parameters = jsonSchema.ToJsonString(
            options: new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            });

        return ChatTool.CreateFunctionTool(
          functionName: GetType().Name,
          functionDescription: Description,
          functionParameters: BinaryData.FromBytes(Encoding.UTF8.GetBytes(parameters))
        );
    }
}
