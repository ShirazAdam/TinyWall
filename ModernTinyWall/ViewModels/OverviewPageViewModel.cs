using CommunityToolkit.Mvvm.Input;
using ModernTinyWall.Models;
using ModernTinyWall.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ModernTinyWall.ViewModels;

internal sealed class OverviewPageViewModel : INotifyPropertyChanged
{
    private const int NetworkActivitySampleLimit = 300;
    private static readonly string[] RateUnits = ["B/s", "KB/s", "MB/s", "GB/s"];

    private readonly IFirewallModeService _firewallModeService;
    private NetworkTotals _previousNetworkTotals;
    private string _statusMessage = "Select a firewall mode to continue the migration workflow.";
    private string _downloadRate = "Download: 0 B/s";
    private string _uploadRate = "Upload: 0 B/s";
    private readonly AsyncRelayCommand<FirewallModeOption> _applyModeCommand;
    private bool _isApplyingMode;

    public OverviewPageViewModel()
        : this(new FirewallModeService())
    {
    }

    internal OverviewPageViewModel(IFirewallModeService firewallModeService)
    {
        _firewallModeService = firewallModeService;
        _applyModeCommand = new AsyncRelayCommand<FirewallModeOption>(ApplyModeAsync, option => option is not null && !IsApplyingMode);
        foreach (var option in _firewallModeService.GetModeOptions())
        {
            ModeOptions.Add(option);
        }

        _previousNetworkTotals = GetNetworkTotals();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<FirewallModeOption> ModeOptions { get; } = [];

    public ObservableCollection<NetworkActivitySample> NetworkActivitySamples { get; } = [];

    public IAsyncRelayCommand<FirewallModeOption> ApplyModeCommand => _applyModeCommand;

    public string DownloadRate
    {
        get => _downloadRate;
        private set
        {
            if (_downloadRate == value)
                return;

            _downloadRate = value;
            OnPropertyChanged();
        }
    }

    public string UploadRate
    {
        get => _uploadRate;
        private set
        {
            if (_uploadRate == value)
                return;

            _uploadRate = value;
            OnPropertyChanged();
        }
    }

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
            ApplyModeCommand.NotifyCanExecuteChanged();
        }
    }

    public async Task ApplyModeAsync(FirewallModeOption? option)
    {
        ArgumentNullException.ThrowIfNull(option);

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

    public void UpdateNetworkActivity()
    {
        var currentTotals = GetNetworkTotals();
        var receivedBytesPerSecond = Math.Max(0, currentTotals.ReceivedBytes - _previousNetworkTotals.ReceivedBytes);
        var sentBytesPerSecond = Math.Max(0, currentTotals.SentBytes - _previousNetworkTotals.SentBytes);

        _previousNetworkTotals = currentTotals;
        AddNetworkActivitySample(receivedBytesPerSecond, sentBytesPerSecond);
    }

    private void AddNetworkActivitySample(long receivedBytesPerSecond, long sentBytesPerSecond)
    {
        NetworkActivitySamples.Add(new NetworkActivitySample(receivedBytesPerSecond, sentBytesPerSecond));

        while (NetworkActivitySamples.Count > NetworkActivitySampleLimit)
        {
            NetworkActivitySamples.RemoveAt(0);
        }

        DownloadRate = $"Download: {FormatRate(receivedBytesPerSecond)}";
        UploadRate = $"Upload: {FormatRate(sentBytesPerSecond)}";
    }

    private static NetworkTotals GetNetworkTotals()
    {
        long receivedBytes = 0;
        long sentBytes = 0;

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            var statistics = networkInterface.GetIPv4Statistics();
            receivedBytes += statistics.BytesReceived;
            sentBytes += statistics.BytesSent;
        }

        return new NetworkTotals(receivedBytes, sentBytes);
    }

    private static string FormatRate(long bytesPerSecond)
    {
        var rate = (double)bytesPerSecond;
        var unitIndex = 0;

        while (rate >= 1024 && unitIndex < RateUnits.Length - 1)
        {
            rate /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{rate:0} {RateUnits[unitIndex]}" : $"{rate:0.0} {RateUnits[unitIndex]}";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal sealed record NetworkActivitySample(long ReceivedBytesPerSecond, long SentBytesPerSecond);

internal sealed record NetworkTotals(long ReceivedBytes, long SentBytes);
