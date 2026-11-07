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
namespace ModernTinyWall.ViewModels;

public sealed class OverviewPageViewModel : INotifyPropertyChanged
{
    private const int NetworkActivitySampleLimit = 300;
    private static readonly string[] RateUnits = ["B/s", "KB/s", "MB/s", "GB/s"];

    private readonly IFirewallModeService _firewallModeService;
    private NetworkTotals? _previousNetworkTotals;
    private (long Received, long Sent)? _previousAdapterTotals;
    private bool _usingAdapterFallback;
    private long _previousSampleTimestamp;
    private int _samplingGeneration;
    private bool _isSampling;
    private readonly Controller _controller = new("TinyWallController");
    private string _localDownloadRate = "0 B/s";
    private string _localUploadRate = "0 B/s";
    private string _networkActivityStatus = "Waiting for traffic tracking...";
    private string _statusMessage = "Choose a protection mode to apply.";
    private string _downloadRate = "0 B/s";
    private string _uploadRate = "0 B/s";
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
                _previousAdapterTotals = null;
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
            var timestamp = Stopwatch.GetTimestamp();
            if (_previousNetworkTotals is { } previous && previous.TrackingStarted == totals.TrackingStarted
                && received >= previous.ReceivedBytes && sent >= previous.SentBytes
                && localReceived >= previous.LocalReceivedBytes && localSent >= previous.LocalSentBytes)
            {
                var elapsed = Stopwatch.GetElapsedTime(_previousSampleTimestamp, timestamp).TotalSeconds;
                var sample = NetworkActivitySample.FromTotals(previous, totals, elapsed);
                AddNetworkActivitySample(sample);
            }

            _previousNetworkTotals = totals;
            _previousSampleTimestamp = timestamp;
            _networkActivityStatus = snapshot.Warning ?? "Internet and local TCP/UDP traffic";
            OnPropertyChanged(nameof(NetworkActivityStatus));
        }
        catch (Exception ex)
        {
            if (generation != _samplingGeneration)
                return;
            Debug.WriteLine($"Could not sample network activity: {ex}");
            _previousNetworkTotals = null;
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
                var timestamp = Stopwatch.GetTimestamp();
                if (_previousAdapterTotals is { } previous && received >= previous.Received && sent >= previous.Sent)
                    AddNetworkActivitySample(NetworkActivitySample.FromAdapterTotals(previous.Received, previous.Sent,
                        received, sent, Stopwatch.GetElapsedTime(_previousSampleTimestamp, timestamp).TotalSeconds));
                else
                {
                    TotalDownloadRate = TotalUploadRate = "Waiting...";
                    OnPropertyChanged(nameof(TotalDownloadRate));
                    OnPropertyChanged(nameof(TotalUploadRate));
                }
                _previousAdapterTotals = (received, sent);
                _previousSampleTimestamp = timestamp;
                _networkActivityStatus = $"Internet and local tracking unavailable: {ex.Message} Totals show adapter traffic instead.";
            }
            catch (Exception adapterError)
            {
                Debug.WriteLine($"Could not sample adapter traffic: {adapterError}");
                _previousAdapterTotals = null;
                NetworkActivitySamples.Clear();
                TotalDownloadRate = TotalUploadRate = "Unavailable";
                OnPropertyChanged(nameof(TotalDownloadRate));
                OnPropertyChanged(nameof(TotalUploadRate));
                _networkActivityStatus = $"Traffic unavailable: {ex.Message} Adapter counters: {adapterError.Message}";
            }
            OnPropertyChanged(nameof(NetworkActivityStatus));
        }
        finally
        {
            _isSampling = false;
        }
    }

    public void ResetNetworkActivityBaseline()
    {
        _samplingGeneration++;
        _previousNetworkTotals = null;
        _previousAdapterTotals = null;
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
