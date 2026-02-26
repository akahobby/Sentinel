namespace Sentinel.App.ViewModels;

public sealed class HistoryEntry
{
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string When { get; set; } = "";
    public DateTime WhenUtc { get; set; }
}
