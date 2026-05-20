using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ModernTinyWall.ViewModels;

internal sealed class ConnectionsPageViewModel
{
    public ObservableCollection<ConnectionRowViewModel> Connections { get; } = [];
    public string SearchText { get; set; } = string.Empty;

    public Task RefreshAsync(bool showActive, bool showListening, bool showBlocked, string searchText)
    {
        SearchText = searchText;

        // TODO: Port the optimised TinyWall ConnectionsForm data collection into a shared service.
        Connections.Clear();
        Connections.Add(new ConnectionRowViewModel(
            "Migration placeholder",
            "TCP",
            "0",
            "0.0.0.0",
            "0",
            "0.0.0.0",
            showActive || showListening || showBlocked ? "Ready" : "Filtered",
            string.Empty));

        return Task.CompletedTask;
    }
}

internal sealed record ConnectionRowViewModel(string Application, string Protocol, string LocalPort, string LocalAddress, string RemotePort, string RemoteAddress, string State, string Direction);
