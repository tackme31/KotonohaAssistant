using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Web;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.AI.Repositories;

public class Weather
{
    public required DateTime DateTime { get; set; }
    public required string Text { get; set; }
    public required double Temperature { get; set; }
}

public interface IWeatherRepository
{
    Task<List<Weather>> GetWeather(DateTime date, (double lat, double lon) location);
}

public class WeatherRepository(string apiKey, ILogger logger) : IWeatherRepository, IDisposable
{
    private readonly string _apiKey = apiKey;
    private readonly HttpClient _httpClient = new();

    private const int FromHour = 6;
    private const int ToHour = 23;

    public async Task<List<Weather>> GetWeather(DateTime date, (double lat, double lon) location)
    {
        var weathers = new List<Weather>();

        // 過去の天気データを取得
        weathers.AddRange(await FetchPastWeather(date, location));

        // 予報データを取得
        weathers.AddRange(await FetchForecastWeather(date, location));

        return [.. weathers.OrderBy(w => w.DateTime)];
    }

    private async Task<IEnumerable<Weather>> FetchPastWeather(DateTime date, (double lat, double lon) location)
    {
        var pastDateTimes = Enumerable.Range(FromHour, ToHour - FromHour)
            .Select(i => date.Date.AddHours(i))
            .Where(dt => dt < DateTime.Now && dt.Hour < DateTime.Now.Hour);

        var tasks = pastDateTimes.Select(dt => FetchWeatherData<TimeMachineResponse>("onecall/timemachine", dt, location));
        var responses = await Task.WhenAll(tasks);

        return responses
            .Where(res => res?.Data?[0]?.Weather?[0] != null)
            .Select(res =>
            {
                var data = res!.Data![0];
                var weather = data.Weather![0];
                return new Weather
                {
                    DateTime = FromUnixTimestamp(data.Dt).ToLocalTime(),
                    Text = GetWeatherText(weather.Id),
                    Temperature = data.Temp,
                };
            });
    }

    private async Task<IEnumerable<Weather>> FetchForecastWeather(DateTime date, (double lat, double lon) location)
    {
        var response = await FetchWeatherData<OneCallResponse>("onecall", null, location);
        if (response?.Hourly == null)
        {
            return [];
        }

        return response.Hourly
            .Where(forecast =>
            {
                var local = FromUnixTimestamp(forecast.Dt).ToLocalTime();
                return local.Day == date.Day && FromHour <= local.Hour && local.Hour <= ToHour;
            })
            .Select(forecast => new Weather
            {
                DateTime = FromUnixTimestamp(forecast.Dt).ToLocalTime(),
                Text = GetWeatherText(forecast.Weather![0].Id),
                Temperature = forecast.Temp
            });
    }

    private async Task<T?> FetchWeatherData<T>(string endpoint, DateTime? dateTime, (double lat, double lon) location) where T : class
    {
        var queryParams = new Dictionary<string, string>
        {
            ["lat"] = location.lat.ToString(),
            ["lon"] = location.lon.ToString(),
            ["units"] = "metric",
            ["appid"] = _apiKey
        };

        if (endpoint == "onecall/timemachine" && dateTime.HasValue)
        {
            queryParams["dt"] = ToUnixTimestamp(dateTime.Value).ToString();
        }
        else if (endpoint == "onecall")
        {
            queryParams["exclude"] = "minutely,daily,alerts,current";
        }

        var url = CreateGetUrl($"https://api.openweathermap.org/data/3.0/{endpoint}", queryParams);

        try
        {
            var result = await _httpClient.GetFromJsonAsync<T>(url);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex);
            return null;
        }
    }

    private static string GetWeatherText(int id) => id switch
    {
        >= 200 and < 300 => "雷雨",
        >= 300 and < 400 => "小雨",
        >= 500 and < 600 => "雨",
        >= 600 and < 700 => "雪",
        >= 700 and < 800 => "霧",
        >= 800 and < 803 => "晴れ",
        >= 803 and < 900 => "曇り",
        _ => "不明"
    };

    private static long ToUnixTimestamp(DateTime dateTime)
    {
        var utcDateTime = dateTime.ToUniversalTime();
        return (long)(utcDateTime - new DateTime(1970, 1, 1)).TotalSeconds;
    }

    private static DateTime FromUnixTimestamp(long unixTime)
    {
        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTime);
    }

    private static string CreateGetUrl(string baseUrl, IDictionary<string, string> queryParams)
    {
        var sb = new StringBuilder(baseUrl);
        if (queryParams.Any())
        {
            sb.Append("?");
            sb.Append(string.Join("&", queryParams.Select(kvp =>
                $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}")));
        }

        return sb.ToString();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private class TimeMachineResponse
    {
        [JsonPropertyName("data")]
        public TimeMachineData[]? Data { get; set; }
    }

    private class TimeMachineData
    {
        [JsonPropertyName("weather")]
        public WeatherData[]? Weather { get; set; }

        [JsonPropertyName("dt")]
        public long Dt { get; set; }

        [JsonPropertyName("temp")]
        public double Temp { get; set; }
    }

    private class OneCallResponse
    {
        [JsonPropertyName("hourly")]
        public OneCallHourlyForecast[]? Hourly { get; set; }
    }

    private class OneCallHourlyForecast
    {
        [JsonPropertyName("weather")]
        public WeatherData[]? Weather { get; set; }

        [JsonPropertyName("dt")]
        public long Dt { get; set; }

        [JsonPropertyName("temp")]
        public double Temp { get; set; }
    }

    private class WeatherData
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("main")]
        public string? Main { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}

