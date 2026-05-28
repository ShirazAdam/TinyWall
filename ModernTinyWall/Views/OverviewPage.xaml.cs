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

    public OverviewPageViewModel ViewModel { get; } = new();

    public OverviewPage()
    {
        InitializeComponent();
        Loaded += OverviewPage_Loaded;
        Unloaded += OverviewPage_Unloaded;
        _networkActivityTimer.Tick += NetworkActivityTimer_Tick;
        ViewModel.NetworkActivitySamples.CollectionChanged += NetworkActivitySamples_CollectionChanged;
    }

    private void OverviewPage_Loaded(object sender, RoutedEventArgs e)
    {
        _networkActivityTimer.Start();
        UpdateNetworkActivityGraph();
    }

    private void OverviewPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _networkActivityTimer.Stop();
        _networkActivityTimer.Tick -= NetworkActivityTimer_Tick;
        ViewModel.NetworkActivitySamples.CollectionChanged -= NetworkActivitySamples_CollectionChanged;
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
        if (NetworkActivityCanvas.ActualWidth <= 0 || NetworkActivityCanvas.ActualHeight <= 0)
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
