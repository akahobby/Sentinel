using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sentinel.App.ViewModels;
using Sentinel.Core.Formatting;
using Sentinel.Core.Models;

namespace Sentinel.App.Pages;

public sealed partial class ProcessesPage : Page
{
    private ProcessesViewModel? _viewModel;

    public ProcessesPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel = (App.Services?.GetService(typeof(Sentinel.Core.Interfaces.IProcessCollector)) is Sentinel.Core.Interfaces.IProcessCollector collector)
            ? new ProcessesViewModel(collector)
            : null;
        if (_viewModel != null)
        {
            DataContext = _viewModel;
            _ = _viewModel.RefreshCommand.ExecuteAsync(null);
        }
    }

    private void ProcessList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProcessList.SelectedItem is ProcessRowViewModel row && row.Process != null)
        {
            var p = row.Process;
            DetailsPane.Visibility = Visibility.Visible;
            DetailsName.Text = p.Name;
            DetailsPid.Text = $"PID: {p.Pid}";
            DetailsPath.Text = p.Path ?? "(no path)";
            DetailsCpuMem.Text = $"CPU: {UsageFormat.CpuPercent(p.CpuPercent)}   Memory: {UsageFormat.MemoryMb(p.MemoryMb)}";
            DetailsGpuNet.Text = $"GPU: {UsageFormat.GpuPercent(p.GpuPercent)}   Network: {UsageFormat.NetworkKbps(p.NetworkKbps)}";
        }
        else if (ProcessList.SelectedItem is ProcessRowViewModel groupRow && groupRow.IsGroupHeader && groupRow.Group != null)
        {
            var g = groupRow.Group;
            if (g.Children.Count > 0)
            {
                var first = g.Children[0];
                DetailsPane.Visibility = Visibility.Visible;
                DetailsName.Text = g.Name + $" ({g.Children.Count} processes)";
                DetailsPid.Text = $"PIDs: {string.Join(", ", g.Children.Select(c => c.Pid))}";
                DetailsPath.Text = first.Path ?? "(no path)";
                DetailsCpuMem.Text = $"CPU: {UsageFormat.CpuPercent(g.AggregateCpu)} total   Memory: {UsageFormat.MemoryMb(g.AggregateMemoryMb)} total";
                DetailsGpuNet.Text = $"GPU: {UsageFormat.GpuPercent(g.AggregateGpu)} total   Network: {UsageFormat.NetworkKbps(g.AggregateNetworkKbps)} total";
            }
        }
        else
        {
            DetailsPane.Visibility = Visibility.Collapsed;
        }
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not ProcessRowViewModel row || row.Group == null || _viewModel == null)
            return;
        _viewModel.ToggleExpandCommand.Execute(row.Group);
    }

    private void SectionHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not ProcessRowViewModel row || !row.IsSectionHeader || _viewModel == null)
            return;
        _viewModel.ToggleSectionCommand.Execute(row.SectionKind);
    }
}
