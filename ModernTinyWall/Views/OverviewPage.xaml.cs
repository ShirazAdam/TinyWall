using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using ModernTinyWall.Models;
using ModernTinyWall.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ModernTinyWall.Views;

public sealed partial class OverviewPage
{
    private static readonly string[] RateUnits = ["B/s", "KB/s", "MB/s", "GB/s"];

    private readonly DispatcherTimer _networkActivityTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    private readonly List<Line> _downloadLines = [];
    private readonly List<Line> _uploadLines = [];
    private MainWindow? _mainWindow;
    private bool _isPageLoaded;
    private bool _isWindowVisible = true;
    private bool _isMonitoring;

    public OverviewPageViewModel ViewModel { get; } = new();

    public OverviewPage()
    {
        InitializeComponent();
        Loaded += OverviewPage_Loaded;
        Unloaded += OverviewPage_Unloaded;
        _networkActivityTimer.Tick += NetworkActivityTimer_Tick;
    }

    private void OverviewPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isPageLoaded = true;
        _mainWindow = App.MainWindow as MainWindow;
        if (_mainWindow is not null)
        {
            _mainWindow.WindowVisibilityChanged += MainWindow_WindowVisibilityChanged;
            _isWindowVisible = _mainWindow.IsWindowVisible;
        }

        UpdateMonitoringState();
        UpdateNetworkActivityGraph();
    }

    private void OverviewPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _isPageLoaded = false;
        if (_mainWindow is not null)
        {
            _mainWindow.WindowVisibilityChanged -= MainWindow_WindowVisibilityChanged;
            _mainWindow = null;
        }

        UpdateMonitoringState();
    }

    private void MainWindow_WindowVisibilityChanged(bool isVisible)
    {
        _isWindowVisible = isVisible;
        UpdateMonitoringState();
    }

    private void UpdateMonitoringState()
    {
        var shouldMonitor = _isPageLoaded && _isWindowVisible;
        if (_isMonitoring == shouldMonitor)
            return;

        _isMonitoring = shouldMonitor;
        if (shouldMonitor)
        {
            ViewModel.ResetNetworkActivityBaseline();
            ViewModel.NetworkActivitySamples.CollectionChanged += NetworkActivitySamples_CollectionChanged;
            _networkActivityTimer.Start();
            UpdateNetworkActivityGraph();
        }
        else
        {
            _networkActivityTimer.Stop();
            ViewModel.NetworkActivitySamples.CollectionChanged -= NetworkActivitySamples_CollectionChanged;
            NetworkActivityCanvas.Children.Clear();
            _downloadLines.Clear();
            _uploadLines.Clear();
        }
    }

    private void NetworkActivitySamples_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateNetworkActivityGraph();
    }

    private void NetworkActivityTimer_Tick(object? sender, object e)
    {
        ViewModel.UpdateNetworkActivity();
    }

    private void NetworkActivityCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateNetworkActivityGraph();
    }

    private void UpdateNetworkActivityGraph()
    {
        if (!_isMonitoring || NetworkActivityCanvas.ActualWidth <= 0 || NetworkActivityCanvas.ActualHeight <= 0)
            return;

        var samples = ViewModel.NetworkActivitySamples;
        long maxValue = 1;

        foreach (var sample in samples)
        {
            maxValue = Math.Max(maxValue, Math.Max(sample.ReceivedBytesPerSecond, sample.SentBytesPerSecond));
        }

        BandwidthScaleTopText.Text = FormatRate(maxValue);
        BandwidthScaleMiddleText.Text = FormatRate(maxValue / 2);

        UpdateSeries(_downloadLines, samples, maxValue, static sample => sample.ReceivedBytesPerSecond, (Brush)Resources["NetworkDownloadBrush"]);
        UpdateSeries(_uploadLines, samples, maxValue, static sample => sample.SentBytesPerSecond, (Brush)Resources["NetworkUploadBrush"]);
    }

    private void UpdateSeries(List<Line> lines, IReadOnlyList<NetworkActivitySample> samples, long maxValue, Func<NetworkActivitySample, long> valueSelector, Brush brush)
    {
        var requiredLineCount = Math.Max(0, samples.Count - 1);
        EnsureLineCount(lines, requiredLineCount, brush);

        if (requiredLineCount == 0)
        {
            foreach (var line in lines)
            {
                line.Visibility = Visibility.Collapsed;
            }

            return;
        }

        var width = NetworkActivityCanvas.ActualWidth;
        var height = NetworkActivityCanvas.ActualHeight;
        var xStep = width / (samples.Count - 1);

        for (var i = 1; i < samples.Count; i++)
        {
            var line = lines[i - 1];
            line.X1 = (i - 1) * xStep;
            line.Y1 = height - (valueSelector(samples[i - 1]) / (double)maxValue * height);
            line.X2 = i * xStep;
            line.Y2 = height - (valueSelector(samples[i]) / (double)maxValue * height);
            line.Visibility = Visibility.Visible;
        }
    }

    private void EnsureLineCount(List<Line> lines, int requiredLineCount, Brush brush)
    {
        while (lines.Count < requiredLineCount)
        {
            var line = new Line
            {
                Stroke = brush,
                StrokeThickness = 2,
                Visibility = Visibility.Collapsed
            };

            lines.Add(line);
            NetworkActivityCanvas.Children.Add(line);
        }

        for (var i = requiredLineCount; i < lines.Count; i++)
        {
            lines[i].Visibility = Visibility.Collapsed;
        }
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
}
