using System.Globalization;
using System.Windows.Data;

namespace KotonohaAssistant.Alarm.Converters;

public class TotalSecondsToDisplayTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long totalSeconds)
        {
            return "00時00分";
        }

        var ts = TimeSpan.FromSeconds(totalSeconds);
        return $"{(int)ts.TotalHours:D2}時{ts.Minutes:D2}分";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
