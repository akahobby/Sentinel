using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Sentinel.Core.Formatting;

namespace Sentinel.App.ViewModels;

/// <summary>One row in the process list: either a group header (expandable) or a single process (or child process).</summary>
public sealed partial class ProcessRowViewModel : ObservableObject
{
    [ObservableProperty] private bool _isGroupHeader;
    [ObservableProperty] private string _displayName = "";
    [ObservableProperty] private string _subtitle = ""; // e.g. "3 processes" or PID
    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private double _memoryMb;
    [ObservableProperty] private double _gpuPercent;
    [ObservableProperty] private double _networkKbps;
    [ObservableProperty] private int _indentLevel; // 0 = top-level, 1 = child under group
    [ObservableProperty] private bool _isExpanded = true;
    [ObservableProperty] private bool _isSectionHeader;
    [ObservableProperty] private string _sectionTitle = "";
    [ObservableProperty] private bool _sectionIsWindows;
    [ObservableProperty] private bool _sectionIsExpanded = true;
    /// <summary>0 = Currently in use (Apps), 1 = Background, 2 = Windows.</summary>
    [ObservableProperty] private int _sectionKind;

    /// <summary>Null for group headers; set for single/child rows.</summary>
    public Sentinel.Core.Models.ProcessInfo? Process { get; set; }

    /// <summary>When this row is a group header, the group (for expand/collapse).</summary>
    public ProcessGroupViewModel? Group { get; set; }

    public bool IsSingleProcess => Process != null && !IsGroupHeader;

    /// <summary>ChevronDown when expanded, ChevronRight when collapsed (for group header).</summary>
    public string ExpandChevron => IsGroupHeader ? (IsExpanded ? "\uE70D" : "\uE76C") : "";

    /// <summary>Collapsed when group header (we show expand button), Visible for single/child row (spacer for alignment).</summary>
    public Visibility SingleRowSpacerVisibility => IsGroupHeader ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Visible when group header so expand button shows, else Collapsed.</summary>
    public Visibility GroupHeaderButtonVisibility => IsGroupHeader ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Visible when this row is a section header (Windows / Background processes).</summary>
    public Visibility SectionHeaderVisibility => IsSectionHeader ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Visible when not a section header (for normal content).</summary>
    public Visibility ContentVisibility => IsSectionHeader ? Visibility.Collapsed : Visibility.Visible;

    public string FormattedMemory => UsageFormat.MemoryMb(MemoryMb);
    public string FormattedCpu => UsageFormat.CpuPercent(CpuPercent);
    public string FormattedGpu => UsageFormat.GpuPercent(GpuPercent);
    public string FormattedNetwork => UsageFormat.NetworkKbps(NetworkKbps);

    /// <summary>Chevron for section header: down when expanded, right when collapsed.</summary>
    public string SectionChevron => IsSectionHeader && SectionIsExpanded ? "\uE70D" : (IsSectionHeader ? "\uE76C" : "");

    /// <summary>Segoe Fluent icon for section: Apps = Application, Background = Document, Windows = Setting.</summary>
    public string SectionIcon => IsSectionHeader ? (SectionKind == 0 ? "\uE8A1" : SectionKind == 1 ? "\uE8A5" : "\uE713") : "";
}
