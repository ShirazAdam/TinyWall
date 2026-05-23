using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using ModernTinyWall.Models;
using ModernTinyWall.ViewModels;
using System;
using System.Collections.Specialized;
using System.Linq;

namespace ModernTinyWall.Views;

public sealed partial class OverviewPage : Page
{
    private readonly DispatcherTimer _networkActivityTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    internal OverviewPageViewModel ViewModel { get; } = new();

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

    private async void ModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: FirewallModeOption option })
        {
            await ViewModel.ApplyModeAsync(option);
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
        if (NetworkActivityCanvas.ActualWidth <= 0 || NetworkActivityCanvas.ActualHeight <= 0)
            return;

        var samples = ViewModel.NetworkActivitySamples.ToArray();
        var maxValue = Math.Max(1, samples.DefaultIfEmpty().Max(sample => Math.Max(sample?.ReceivedBytesPerSecond ?? 0, sample?.SentBytesPerSecond ?? 0)));

        BandwidthScaleTopText.Text = FormatRate(maxValue);
        BandwidthScaleMiddleText.Text = FormatRate(maxValue / 2);

        NetworkActivityCanvas.Children.Clear();
        AddSeries(samples, maxValue, sample => sample.ReceivedBytesPerSecond, (Brush)Resources["NetworkDownloadBrush"]);
        AddSeries(samples, maxValue, sample => sample.SentBytesPerSecond, (Brush)Resources["NetworkUploadBrush"]);
    }

    private void AddSeries(NetworkActivitySample[] samples, long maxValue, Func<NetworkActivitySample, long> valueSelector, Brush brush)
    {
        if (samples.Length < 2)
            return;

        var width = NetworkActivityCanvas.ActualWidth;
        var height = NetworkActivityCanvas.ActualHeight;
        var xStep = width / (samples.Length - 1);

        for (var i = 1; i < samples.Length; i++)
        {
            NetworkActivityCanvas.Children.Add(new Line
            {
                X1 = (i - 1) * xStep,
                Y1 = height - (valueSelector(samples[i - 1]) / (double)maxValue * height),
                X2 = i * xStep,
                Y2 = height - (valueSelector(samples[i]) / (double)maxValue * height),
                Stroke = brush,
                StrokeThickness = 2
            });
        }
    }

    private static string FormatRate(long bytesPerSecond)
    {
        string[] units = ["B/s", "KB/s", "MB/s", "GB/s"];
        var rate = (double)bytesPerSecond;
        var unitIndex = 0;

        while (rate >= 1024 && unitIndex < units.Length - 1)
        {
            rate /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{rate:0} {units[unitIndex]}" : $"{rate:0.0} {units[unitIndex]}";
    }
}
