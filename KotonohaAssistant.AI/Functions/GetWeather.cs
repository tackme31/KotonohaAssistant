using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Utils;
using System.Text;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class GetWeather(IWeatherRepository weatherRepository) : ToolFunction
{
    public override string Description => """
この関数は、指定された日の天気を取得するために呼び出されます。
天気を尋ねられた際に以下のような依頼を受けて実行されます。

## 呼び出される例

- 「今日の天気は？」
- 「今日は傘必要そう？」
- 「今って晴れてる？」

""";

    public override string Parameters => """
{
    "type": "object",
    "properties": {
        "date": {
            "type": "string",
            "description": "天気を取得する日にち。形式はyyyy/MM/dd"
        }
    },
    "required": [ "date" ],
    "additionalProperties": false
}
""";

    private readonly IWeatherRepository _weatherRepository = weatherRepository;

    public override bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments)
    {
        arguments = new Dictionary<string, object>();

        var date = doc.RootElement.GetDateTimeProperty("date");
        if (date is null)
        {
            return false;
        }
        arguments["date"] = date.Value;

        return true;
    }

    public override async Task<string> Invoke(IDictionary<string, object> arguments, IReadOnlyConversationState state)
    {
        var date = (DateTime)arguments["date"];
        var weathers = await _weatherRepository.GetWeather(date, (35.6506, 139.5406));
        if (weathers is null or [])
        {
            return "天気が取得できませんでした";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"## {date:M月d日の天気}");
        foreach (var weather in weathers)
        {
            sb.AppendLine($"- {weather.DateTime:HH}時: {weather.Text}");
        }

        return sb.ToString();
    }
}