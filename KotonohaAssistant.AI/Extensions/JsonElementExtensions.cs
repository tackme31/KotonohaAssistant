using System.Text.Json;

namespace KotonohaAssistant.AI.Extensions;

static class JsonElementExtensions
{
    public static string? GetStringProperty(this JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        return prop.GetString();
    }

    public static int? GetIntProperty(this JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        return prop.GetInt32();
    }

    public static TimeSpan? GetTimeSpanProperty(this JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        var value = prop.GetString();
        if (!TimeSpan.TryParse(value, out var timeSpan))
        {
            return null;
        }

        return timeSpan;
    }

    public static DateTime? GetDateTimeProperty(this JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }


        var propValue = prop.GetString();
        if (!DateTime.TryParse(propValue, out var value))
        {
            return null;
        }

        return value;
    }
}