using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Sentinel.App.ViewModels;

/// <summary>Group of processes sharing the same name (e.g. multiple "chrome" instances).</summary>
public sealed partial class ProcessGroupViewModel : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isExpanded = true;
    [ObservableProperty] private ObservableCollection<Sentinel.Core.Models.ProcessInfo> _children = new();

    /// <summary>True if this group is a Windows system process (by name/path).</summary>
    public bool IsWindows { get; set; }

    /// <summary>True if any process in this group has a visible window (app in use).</summary>
    public bool IsApp { get; set; }

    public double AggregateCpu => Children.Sum(c => c.CpuPercent);
    public double AggregateMemoryMb => Children.Sum(c => c.MemoryMb);
    public double AggregateGpu => Children.Sum(c => c.GpuPercent);
    public double AggregateNetworkKbps => Children.Sum(c => c.NetworkKbps);
    public int Count => Children.Count;
    public bool IsMultiple => Children.Count > 1;
}
