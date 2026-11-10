using CommunityToolkit.Mvvm.Input;
using ModernTinyWall.Models;
using ModernTinyWall.Services;
using ModernTinyWall.TinyWall;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
namespace ModernTinyWall.ViewModels;

public sealed class OverviewPageViewModel : INotifyPropertyChanged
{
    private const int NetworkActivitySampleLimit = 300;
    private static readonly string[] RateUnits = ["B/s", "KB/s", "MB/s", "GB/s"];

    private readonly IFirewallModeService _firewallModeService;
    private readonly NetworkActivityBaseline _networkBaseline = new();
    private bool _usingAdapterFallback;
    private int _samplingGeneration;
    private bool _isSampling;
    private readonly Controller _controller = new("TinyWallController");
    private string _localDownloadRate = "0 B/s";
    private string _localUploadRate = "0 B/s";
    private string _networkActivityStatus = "Waiting for traffic tracking...";
    private string _statusMessage = string.Empty;
    private string _downloadRate = "0 B/s";
    private string _uploadRate = "0 B/s";
    private readonly AsyncRelayCommand<FirewallModeOption> _applyModeCommand;
    private bool _isApplyingMode;
    private bool _isRefreshingSummary;
    public string CurrentModeName { get; private set; } = "Checking...";
    public string ProtectionStatus { get; private set; } = "Checking protection";
    public string ProtectionDetail { get; private set; } = "Reading the TinyWall service state.";
    public string ActiveConnections { get; private set; } = "Checking...";
    public string ConnectionsDetail { get; private set; } = "Established IPv4 and IPv6 TCP connections.";
    public string BlockedToday { get; private set; } = "Checking...";
    public string BlockedDetail { get; private set; } = "Reading observed blocked traffic history.";
    public SolidColorBrush ProtectionBrush { get; private set; } = new(Colors.Gray);

    public async Task RefreshSummaryAsync()
    {
        if (_isRefreshingSummary)
            return;
        _isRefreshingSummary = true;
        var generation = _samplingGeneration;
        try
        {
            var result = await Task.Run(() =>
            {
                var changeset = Guid.Empty;
                var response = _controller.GetServerConfig(out _, out var state, ref changeset);
                return (response, state);
            });
            if (generation != _samplingGeneration)
                return;
            if (result.response == MessageType.GET_SETTINGS && result.state is not null)
                SetCurrentMode(result.state.Mode);
            else
            {
                SetCurrentMode(FirewallMode.Unknown);
                ProtectionDetail = $"Could not read the TinyWall service state: {result.response}.";
                OnPropertyChanged(nameof(ProtectionDetail));
            }
        }
        catch (Exception ex)
        {
            if (generation != _samplingGeneration)
                return;
            Debug.WriteLine($"Could not refresh protection summary: {ex}");
            SetCurrentMode(FirewallMode.Unknown);
            ProtectionDetail = $"Could not read protection status: {ex.Message}";
            OnPropertyChanged(nameof(ProtectionDetail));
        }
        finally
        {
            try
            {
                await RefreshActiveConnectionsAsync(generation);
            }
            finally
            {
                try
                {
                    await RefreshBlockedTrafficAsync(generation);
                }
                finally
                {
                    _isRefreshingSummary = false;
                }
            }
        }
    }

    private async Task RefreshBlockedTrafficAsync(int generation)
    {
        if (generation != _samplingGeneration)
            return;
        try
        {
            var snapshot = await Task.Run(_controller.ReadBlockedTraffic);
            if (generation != _samplingGeneration)
                return;
            var display = BlockedTrafficDisplay.FromSnapshot(snapshot, DateOnly.FromDateTime(DateTime.Now));
            BlockedToday = display.Count;
            BlockedDetail = display.Detail;
        }
        catch (Exception ex)
        {
            if (generation != _samplingGeneration)
                return;
            Debug.WriteLine($"Could not refresh blocked traffic: {ex}");
            BlockedToday = "Unavailable";
            BlockedDetail = $"Could not read blocked traffic history: {ex.Message}";
        }
        OnPropertyChanged(nameof(BlockedToday));
        OnPropertyChanged(nameof(BlockedDetail));
    }

    private async Task RefreshActiveConnectionsAsync(int generation)
    {
        if (generation != _samplingGeneration)
            return;
        try
        {
            var count = await Task.Run(() => IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpConnections().Count(connection => connection.State == TcpState.Established));
            if (generation != _samplingGeneration)
                return;
            ActiveConnections = count.ToString("N0");
            ConnectionsDetail = "Established IPv4 and IPv6 TCP connections. UDP has no established connection state.";
        }
        catch (Exception ex)
        {
            if (generation != _samplingGeneration)
                return;
            Debug.WriteLine($"Could not count active connections: {ex}");
            ActiveConnections = "Unavailable";
            ConnectionsDetail = $"Could not count active connections: {ex.Message}";
        }
        OnPropertyChanged(nameof(ActiveConnections));
        OnPropertyChanged(nameof(ConnectionsDetail));
    }

    private void SetCurrentMode(FirewallMode mode)
    {
        var modernMode = mode switch
        {
            FirewallMode.Normal => ModernFirewallMode.Normal,
            FirewallMode.AllowOutgoing => ModernFirewallMode.AllowOutgoing,
            FirewallMode.BlockAll => ModernFirewallMode.BlockAll,
            FirewallMode.Disabled => ModernFirewallMode.Disabled,
            FirewallMode.Learning => ModernFirewallMode.Learning,
            _ => ModernFirewallMode.Unknown
        };
        var selected = ModeOptions.FirstOrDefault(option => option.Mode == modernMode);
        foreach (var option in ModeOptions)
            option.IsSelected = option == selected;
        CurrentModeName = selected?.DisplayName ?? "Unavailable";
        ProtectionStatus = modernMode switch
        {
            ModernFirewallMode.Normal => "Protection active",
            ModernFirewallMode.BlockAll => "All traffic blocked",
            ModernFirewallMode.AllowOutgoing => "Outgoing allowed",
            ModernFirewallMode.Disabled => "Protection disabled",
            ModernFirewallMode.Learning => "Learning mode",
            _ => "Service unavailable"
        };
        ProtectionDetail = selected?.Description ?? "The TinyWall service is unavailable. Protection status cannot be verified.";
        ProtectionBrush = new SolidColorBrush(modernMode switch
        {
            ModernFirewallMode.Normal => ColorHelper.FromArgb(255, 77, 221, 145),
            ModernFirewallMode.AllowOutgoing => ColorHelper.FromArgb(255, 0, 190, 163),
            ModernFirewallMode.BlockAll or ModernFirewallMode.Disabled => ColorHelper.FromArgb(255, 255, 83, 104),
            ModernFirewallMode.Learning => ColorHelper.FromArgb(255, 255, 215, 0),
            _ => Colors.Gray
        });
        OnPropertyChanged(nameof(CurrentModeName));
        OnPropertyChanged(nameof(ProtectionStatus));
        OnPropertyChanged(nameof(ProtectionDetail));
        OnPropertyChanged(nameof(ProtectionBrush));
    }

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

    public string LocalDownloadRate => _localDownloadRate;
    public string LocalUploadRate => _localUploadRate;
    public string TotalDownloadRate { get; private set; } = "0 B/s";
    public string TotalUploadRate { get; private set; } = "0 B/s";
    public string NetworkActivityStatus => _networkActivityStatus;
    public string TrafficSourceLabel => _usingAdapterFallback
        ? "Last 5 minutes - adapter totals only"
        : "Last 5 minutes";

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
                return;

            _statusMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ModeStatusVisibility));
        }
    }
    public Microsoft.UI.Xaml.Visibility ModeStatusVisibility => string.IsNullOrEmpty(StatusMessage)
        ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

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
            await RefreshSummaryAsync();
        }
        finally
        {
            IsApplyingMode = false;
        }
    }

    public async Task UpdateNetworkActivityAsync()
    {
        if (_isSampling)
            return;

        _isSampling = true;
        var generation = _samplingGeneration;
        try
        {
            var snapshot = await Task.Run(() => _controller.ReadDataUsage(DataUsagePeriod.AllTime));
            if (generation != _samplingGeneration)
                return;

            if (snapshot.Error is not null)
                throw new InvalidOperationException(snapshot.Error);

            if (_usingAdapterFallback)
            {
                _usingAdapterFallback = false;
                NetworkActivitySamples.Clear();
                TotalDownloadRate = TotalUploadRate = "Waiting...";
                OnPropertyChanged(nameof(TotalDownloadRate));
                OnPropertyChanged(nameof(TotalUploadRate));
            }

            long received = 0, sent = 0, localReceived = 0, localSent = 0;
            foreach (var application in snapshot.Applications)
            {
                received = checked(received + application.InternetReceived);
                sent = checked(sent + application.InternetSent);
                localReceived = checked(localReceived + application.LocalReceived);
                localSent = checked(localSent + application.LocalSent);
            }

            var totals = new NetworkTotals(received, sent, localReceived, localSent, snapshot.TrackingStarted);
            if (_networkBaseline.RecordClassified(totals, Stopwatch.GetTimestamp()) is { } sample)
            {
                AddNetworkActivitySample(sample);
            }
            else
            {
                DownloadRate = UploadRate = _localDownloadRate = _localUploadRate = "Waiting...";
                TotalDownloadRate = TotalUploadRate = "Waiting...";
                OnPropertyChanged(nameof(LocalDownloadRate));
                OnPropertyChanged(nameof(LocalUploadRate));
                OnPropertyChanged(nameof(TotalDownloadRate));
                OnPropertyChanged(nameof(TotalUploadRate));
            }

            _networkActivityStatus = snapshot.Warning ?? "Internet and local TCP/UDP traffic";
            OnPropertyChanged(nameof(NetworkActivityStatus));
            OnPropertyChanged(nameof(TrafficSourceLabel));
        }
        catch (Exception ex)
        {
            if (generation != _samplingGeneration)
                return;
            Debug.WriteLine($"Could not sample network activity: {ex}");
            DownloadRate = UploadRate = "Unavailable";
            _localDownloadRate = _localUploadRate = "Unavailable";
            OnPropertyChanged(nameof(LocalDownloadRate));
            OnPropertyChanged(nameof(LocalUploadRate));
            if (!_usingAdapterFallback)
            {
                _usingAdapterFallback = true;
                NetworkActivitySamples.Clear();
            }
            try
            {
                long received = 0, sent = 0;
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up ||
                        adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                        continue;
                    var statistics = adapter.GetIPv4Statistics();
                    received = checked(received + statistics.BytesReceived);
                    sent = checked(sent + statistics.BytesSent);
                }
                if (_networkBaseline.RecordAdapter(received, sent, Stopwatch.GetTimestamp()) is { } sample)
                    AddNetworkActivitySample(sample);
                else
                {
                    TotalDownloadRate = TotalUploadRate = "Waiting...";
                    OnPropertyChanged(nameof(TotalDownloadRate));
                    OnPropertyChanged(nameof(TotalUploadRate));
                }
                _networkActivityStatus = $"Internet and local tracking unavailable: {ex.Message} Totals show adapter traffic instead.";
            }
            catch (Exception adapterError)
            {
                Debug.WriteLine($"Could not sample adapter traffic: {adapterError}");
                _networkBaseline.Reset();
                NetworkActivitySamples.Clear();
                TotalDownloadRate = TotalUploadRate = "Unavailable";
                OnPropertyChanged(nameof(TotalDownloadRate));
                OnPropertyChanged(nameof(TotalUploadRate));
                _networkActivityStatus = $"Traffic unavailable: {ex.Message} Adapter counters: {adapterError.Message}";
            }
            OnPropertyChanged(nameof(NetworkActivityStatus));
            OnPropertyChanged(nameof(TrafficSourceLabel));
        }
        finally
        {
            _isSampling = false;
        }
    }

    public void ResetNetworkActivityBaseline()
    {
        _samplingGeneration++;
        _networkBaseline.Reset();
        _usingAdapterFallback = false;
        NetworkActivitySamples.Clear();
        DownloadRate = UploadRate = "0 B/s";
        _localDownloadRate = _localUploadRate = "0 B/s";
        TotalDownloadRate = TotalUploadRate = "0 B/s";
        _networkActivityStatus = "Waiting for traffic tracking...";
        OnPropertyChanged(nameof(LocalDownloadRate));
        OnPropertyChanged(nameof(LocalUploadRate));
        OnPropertyChanged(nameof(TotalDownloadRate));
        OnPropertyChanged(nameof(TotalUploadRate));
        OnPropertyChanged(nameof(NetworkActivityStatus));
        OnPropertyChanged(nameof(TrafficSourceLabel));
    }

    private void AddNetworkActivitySample(NetworkActivitySample sample)
    {
        NetworkActivitySamples.Add(sample);

        while (NetworkActivitySamples.Count > NetworkActivitySampleLimit
            || NetworkActivitySamples[0].Timestamp < sample.Timestamp.AddMinutes(-5))
        {
            NetworkActivitySamples.RemoveAt(0);
        }

        DownloadRate = sample.IsClassified ? FormatRate(sample.ReceivedBytesPerSecond) : "Unavailable";
        UploadRate = sample.IsClassified ? FormatRate(sample.SentBytesPerSecond) : "Unavailable";
        _localDownloadRate = sample.IsClassified ? FormatRate(sample.LocalReceivedBytesPerSecond) : "Unavailable";
        _localUploadRate = sample.IsClassified ? FormatRate(sample.LocalSentBytesPerSecond) : "Unavailable";
        TotalDownloadRate = FormatRate(sample.TotalReceivedBytesPerSecond);
        TotalUploadRate = FormatRate(sample.TotalSentBytesPerSecond);
        OnPropertyChanged(nameof(LocalDownloadRate));
        OnPropertyChanged(nameof(LocalUploadRate));
        OnPropertyChanged(nameof(TotalDownloadRate));
        OnPropertyChanged(nameof(TotalUploadRate));
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
