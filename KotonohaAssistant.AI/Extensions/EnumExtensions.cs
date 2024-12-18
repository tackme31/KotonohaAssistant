using KotonohaAssistant.Core;

namespace KotonohaAssistant.AI.Extensions;

internal static class EnumExtensions
{
    public static string ToDisplayName(this Kotonoha sister)
    {
        return sister switch
        {
            Kotonoha.Akane => "茜",
            Kotonoha.Aoi => "葵",
            _ => string.Empty
        };
    }

    public static Kotonoha Switch(this Kotonoha sister)
    {
        return sister switch
        {
            Kotonoha.Akane => Kotonoha.Aoi,
            Kotonoha.Aoi => Kotonoha.Akane,
            _ => sister
        };
    }
}
