using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace KotonohaAssistant.Alarm.Converters;

public class FilePathToFileNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string filePath)
        {
            return "00時00分";
        }

        var fileName = Path.GetFileName(filePath);
        return fileName;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
