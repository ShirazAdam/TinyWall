using ModernTinyWall.Models;
using ModernTinyWall.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ModernTinyWall.ViewModels;

internal sealed class OverviewPageViewModel : INotifyPropertyChanged
{
    private readonly IFirewallModeService _firewallModeService;
    private string _statusMessage = "Select a firewall mode to continue the migration workflow.";
    private bool _isApplyingMode;

    public OverviewPageViewModel()
        : this(new FirewallModeService())
    {
    }

    internal OverviewPageViewModel(IFirewallModeService firewallModeService)
    {
        _firewallModeService = firewallModeService;
        foreach (var option in _firewallModeService.GetModeOptions())
        {
            ModeOptions.Add(option);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<FirewallModeOption> ModeOptions { get; } = [];

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
                return;

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public bool IsApplyingMode
    {
        get => _isApplyingMode;
        private set
        {
            if (_isApplyingMode == value)
                return;

            _isApplyingMode = value;
            OnPropertyChanged();
        }
    }

    public async Task ApplyModeAsync(FirewallModeOption option)
    {
        IsApplyingMode = true;
        StatusMessage = $"Applying {option.DisplayName.ToLowerInvariant()}...";

        try
        {
            var result = await _firewallModeService.SetModeAsync(option.Mode);
            StatusMessage = result.Message;
        }
        finally
        {
            IsApplyingMode = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
