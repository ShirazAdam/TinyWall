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

    private readonly Path _downloadPath = new() { StrokeThickness = 2 };
    private readonly Path _uploadPath = new() { StrokeThickness = 2 };
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
            _downloadPath.Data = null;
            _uploadPath.Data = null;
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

        UpdateSeries(_downloadPath, samples, maxValue, static sample => sample.ReceivedBytesPerSecond, (Brush)Resources["NetworkDownloadBrush"]);
        UpdateSeries(_uploadPath, samples, maxValue, static sample => sample.SentBytesPerSecond, (Brush)Resources["NetworkUploadBrush"]);
    }

    private void UpdateSeries(Path path, IReadOnlyList<NetworkActivitySample> samples, long maxValue, Func<NetworkActivitySample, long> valueSelector, Brush brush)
    {
        if (samples.Count < 2)
        {
            path.Data = null;
            return;
        }

        var width = NetworkActivityCanvas.ActualWidth;
        var height = NetworkActivityCanvas.ActualHeight;
        var xStep = width / (samples.Count - 1);
        var figure = new PathFigure
        {
            StartPoint = new(0, height - (valueSelector(samples[0]) / (double)maxValue * height)),
            IsFilled = false
        };
        var previousPoint = figure.StartPoint;

        for (var i = 1; i < samples.Count; i++)
        {
            var x = i * xStep;
            var y = height - (valueSelector(samples[i]) / (double)maxValue * height);
            var midpointX = (previousPoint.X + x) / 2;
            // Horizontal tangents join smoothly without overshooting either sample.
            var segment = new BezierSegment
            {
                Point1 = new(midpointX, previousPoint.Y),
                Point2 = new(midpointX, y),
                Point3 = new(x, y)
            };
            figure.Segments.Add(segment);
            previousPoint = segment.Point3;
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        path.Data = geometry;
        path.Stroke = brush;
        if (!NetworkActivityCanvas.Children.Contains(path))
            NetworkActivityCanvas.Children.Add(path);
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
