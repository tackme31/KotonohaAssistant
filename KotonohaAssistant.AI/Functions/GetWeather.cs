using System.ComponentModel;
using System.Text;
using System.Text.Json;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.AI.Functions;

public class GetWeather(IPromptRepository promptRepository, IWeatherRepository weatherRepository, (double lat, double lon) location, ILogger logger)
    : ToolFunction(logger)
{
    private record Parameters(
        [property: Description("天気を取得する日にち。形式はyyyy/MM/dd")]
        string Date);

    public override string Description => promptRepository.GetWeatherDescription;
    protected override Type ParameterType => typeof(Parameters);

    private readonly IWeatherRepository _weatherRepository = weatherRepository;
    private readonly (double lat, double lon) _location = location;

    public override async Task<string?> Invoke(JsonDocument argumentsDoc, IReadOnlyConversationState state)
    {
        var args = Deserialize<Parameters>(argumentsDoc);
        if (args is null)
        {
            return null;
        }

        if (!DateTime.TryParse(args.Date, out var date))
        {
            return null;
        }

        try
        {
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
