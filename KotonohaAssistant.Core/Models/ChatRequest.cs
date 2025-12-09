using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace KotonohaAssistant.Core.Models;

public class ChatRequest
{
    // JSONシリアライズ用の設定
    protected static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented, // インデント付き
        StringEscapeHandling = StringEscapeHandling.Default, // 日本語などの非ASCII文字をそのまま出力
        Converters =
        {
            new StringEnumConverter() // Enumを文字列として扱う
        },
        NullValueHandling = NullValueHandling.Ignore
    };

    public ChatInputType? InputType { get; set; }
    public string? Text { get; set; }
    public string? Today { get; set; }
    public string? CurrentTime { get; set; }

    // JSONから安全にパース
    public static bool TryParse(string input, out ChatRequest? output)
    {
        try
        {
            output = JsonConvert.DeserializeObject<ChatRequest>(input, Settings);
            return output != null;
        }
        catch
        {
            output = null;
            return false;
        }
    }

    // JSON文字列に変換
    public string ToJson()
    {
        return JsonConvert.SerializeObject(this, Settings);
    }
}
