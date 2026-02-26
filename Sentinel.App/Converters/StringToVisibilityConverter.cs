using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Sentinel.App.Converters;

public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var visible = !string.IsNullOrWhiteSpace(value as string);
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
