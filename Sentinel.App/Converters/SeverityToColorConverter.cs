using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Sentinel.App.Converters;

public sealed class SeverityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Sentinel.Core.Models.Severity severity)
        {
            var color = severity switch
            {
                Sentinel.Core.Models.Severity.Info => Windows.UI.Color.FromArgb(255, 33, 150, 243),
                Sentinel.Core.Models.Severity.Ok => Windows.UI.Color.FromArgb(255, 76, 175, 80),
                Sentinel.Core.Models.Severity.Warn => Windows.UI.Color.FromArgb(255, 255, 152, 0),
                Sentinel.Core.Models.Severity.Fail => Windows.UI.Color.FromArgb(255, 244, 67, 54),
                _ => Windows.UI.Color.FromArgb(255, 136, 136, 136)
            };
            return new SolidColorBrush(color);
        }
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 136, 136, 136));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
