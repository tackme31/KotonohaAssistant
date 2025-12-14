using System.Text;
using System.Text.Json;
using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.AI.Functions;

public class GetWeather(IPromptRepository promptRepository, IWeatherRepository weatherRepository, (double lat, double lon) location, ILogger logger) : ToolFunction(logger)
{
    public override string Description => promptRepository.GetWeatherDescription;

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
    private readonly (double lat, double lon) _location = location;

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
        try
        {
            var date = (DateTime)arguments["date"];
            var weathers = await _weatherRepository.GetWeather(date, _location);
            if (weathers is null or [])
            {
                return "天気情報が見つかりませんでした";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"## {date:M月d日の天気}");
            foreach (var weather in weathers)
            {
                sb.AppendLine($"- {weather.DateTime:HH}時: {weather.Text} ({weather.Temperature}度)");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);

            return "天気が取得できませんでした";
        }
    }
}
