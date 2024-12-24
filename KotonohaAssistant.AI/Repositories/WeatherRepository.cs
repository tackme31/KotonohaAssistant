using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Web;

namespace KotonohaAssistant.AI.Repositories;

public class Weather
{
    public required DateTime DateTime { get; set; }
    public required string Text { get; set; }
}

public interface IWeatherRepository
{
    Task<List<Weather>> GetWeather(DateTime date, (double lat, double lon) location);
}

public class WeatherRepository(string apiKey) : IWeatherRepository, IDisposable
{
    private readonly string _apiKey = apiKey;
    private readonly HttpClient _httpClient = new();

    public async Task<List<Weather>> GetWeather(DateTime date, (double lat, double lon) location)
    {
        const int from = 6;
        const int to = 23;

        var weathers = new List<Weather>();

        // 過去の天気を取得
        var pastDateTimes = Enumerable.Range(from, to - from) // 7時から23時
            .Select(i => date.Date.AddHours(i))
            .Where(dt => dt < DateTime.Now && dt.Hour < DateTime.Now.Hour);
        var tasks = new List<Task<TimeMachineResponse?>>();
        foreach (var dateTime in pastDateTimes)
        {
            var task = FetchWeatherTimestamp(dateTime, location);
            tasks.Add(task);
        }

        var pastWeathers = await Task.WhenAll(tasks);
        foreach (var res in pastWeathers ?? [])
        {
            var data = res?.Data?[0];
            var weather = data?.Weather?[0];
            if (data is null || weather is null)
            {
                continue;
            }

            var local = FromUnitTimestamp(data.Dt).ToLocalTime();
            weathers.Add(new Weather
            {
                DateTime = local,
                Text = GetWeatherText(weather.Id)
            });
        }

        var hourlyForecasts = await FetchForecastForNextThreeDays(location);
        foreach (var forecast in hourlyForecasts?.Hourly ?? [])
        {
            if (forecast.Weather is null)
            {
                continue;
            }

            var local = FromUnitTimestamp(forecast.Dt).ToLocalTime();
            if (local.Day == date.Day && from <= local.Hour && local.Hour <= to)
            {
                weathers.Add(new Weather
                {
                    DateTime = local,
                    Text = GetWeatherText(forecast.Weather[0].Id)
                });
            }
        }

        return [.. weathers.OrderBy(w => w.DateTime)];
    }

    private async Task<OneCallResponse?> FetchForecastForNextThreeDays((double lat, double lon) location)
    {
        var (lat, lon) = location;
        var queryParams = new Dictionary<string, string>
        {
            ["lat"] = lat.ToString(),
            ["lon"] = lon.ToString(),
            ["exclude"] = string.Join(",", ["minutely", "daily", "alerts", "current"]),
            ["units"] = "metric",
            ["appid"] = _apiKey,
        };
        var url = CreateGetUrl("https://api.openweathermap.org/data/3.0/onecall", queryParams);

        try
        {
            var res = await _httpClient.GetAsync(url);
            var content = await res.Content.ReadFromJsonAsync<OneCallResponse>();
            return content;
        }
        catch (Exception ex)
        {
            // TODO: ログ
            return null;
        }
    }

    private async Task<TimeMachineResponse?> FetchWeatherTimestamp(DateTime dateTime, (double lat, double lon) location)
    {
        var (lat, lon) = location;
        var queryParams = new Dictionary<string, string>
        {
            ["lat"] = lat.ToString(),
            ["lon"] = lon.ToString(),
            ["dt"] = ToUnixTimestamp(dateTime).ToString(),
            ["units"] = "metric",
            ["appid"] = _apiKey,
        };
        var url = CreateGetUrl("https://api.openweathermap.org/data/3.0/onecall/timemachine", queryParams);

        try
        {
            var res = await _httpClient.GetAsync(url);
            var content = await res.Content.ReadFromJsonAsync<TimeMachineResponse>();
            return content;
        }
        catch (Exception ex)
        {
            // TODO: ログ
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

    private static DateTime FromUnitTimestamp(long unixTime)
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

