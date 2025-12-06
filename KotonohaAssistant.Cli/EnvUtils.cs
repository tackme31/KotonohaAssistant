using System.Globalization;

namespace KotonohaAssistant.Cli;

public static class EnvUtils
{
    public static string GetStringValueOrDefault(string key, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrEmpty(value) ? defaultValue : value;
    }

    public static double GetDoubleValueOrDefault(string key, double defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
        {
            return result;
        }
        return defaultValue;
    }

    public static bool GetBooleanValueOrDefault(string key, bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (bool.TryParse(value, out bool result))
        {
            return result;
        }
        return defaultValue;
    }

    public static int GetIntValueOrDefault(string key, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (int.TryParse(value, out int result))
        {
            return result;
        }
        return defaultValue;
    }

    public static TimeSpan GetTimeSpanValueOrDefault(string key, TimeSpan defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (TimeSpan.TryParse(value, out TimeSpan result))
        {
            return result;
        }
        return defaultValue;
    }
}
