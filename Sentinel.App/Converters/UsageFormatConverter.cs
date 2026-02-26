using Microsoft.UI.Xaml.Data;
using Sentinel.Core.Formatting;

namespace Sentinel.App.Converters;

public sealed class UsageFormatConverter : IValueConverter
{
    public string Type { get; set; } = "Memory"; // Memory, Cpu, Gpu, Network

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var t = (parameter as string) ?? Type;
        if (value is double d)
            return t switch
            {
                "Cpu" => UsageFormat.CpuPercent(d),
                "Gpu" => UsageFormat.GpuPercent(d),
                "Network" => UsageFormat.NetworkKbps(d),
                _ => UsageFormat.MemoryMb(d)
            };
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
