using Microsoft.UI.Xaml.Data;

namespace Sentinel.App.Converters;

/// <summary>Converts byte count to human-readable size (e.g. 1.2 GB).</summary>
public sealed class BytesToSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        long bytes = value switch
        {
            long l => l,
            int i => i,
            _ => 0
        };
        if (bytes <= 0) return "—";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:N1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):N1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):N2} GB";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
