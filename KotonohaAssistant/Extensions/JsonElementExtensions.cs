using System.Text.Json;

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
}