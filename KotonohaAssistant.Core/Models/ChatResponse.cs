using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace KotonohaAssistant.Core.Models;

public record ChatResponse
{
    // JSONシリアライズ用設定
    protected static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented, // インデント付き
        StringEscapeHandling = StringEscapeHandling.Default, // 日本語をそのまま出力
        Converters =
        {
            new StringEnumConverter() // Enumを文字列として扱う
        },
        NullValueHandling = NullValueHandling.Ignore
    };

    public Kotonoha Assistant { get; set; } = new Kotonoha();
    public string? Text { get; set; }

    // JSONから安全にパース
    public static bool TryParse(string input, out ChatResponse? output)
    {
        try
        {
            output = JsonConvert.DeserializeObject<ChatResponse>(input, Settings);
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
