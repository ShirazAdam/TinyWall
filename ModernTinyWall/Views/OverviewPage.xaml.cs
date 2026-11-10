using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using ModernTinyWall.Models;
using ModernTinyWall.ViewModels;
using ModernTinyWall.Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Numerics;
using Windows.UI.ViewManagement;

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
    private readonly Path _localDownloadPath = new() { StrokeThickness = 2 };
    private readonly Path _localUploadPath = new() { StrokeThickness = 2 };
    private readonly Path _totalDownloadPath = new() { StrokeThickness = 2 };
    private readonly Path _totalUploadPath = new() { StrokeThickness = 2 };
    private readonly Dictionary<Path, Path> _outlines = [];
    private MainWindow? _mainWindow;
    private bool _isPageLoaded;
    private bool _isWindowVisible = true;
    private bool _isMonitoring;
    private readonly IOptionsService _optionsService = new OptionsService();
    private bool _isRestoringSeries;
    private bool _optionsReady;
    private readonly UISettings _uiSettings = new();
    private readonly AccessibilitySettings _accessibilitySettings = new();
    private long _lastSummaryRefresh;

    public OverviewPageViewModel ViewModel { get; } = new();

    public OverviewPage()
    {
        InitializeComponent();
        Loaded += OverviewPage_Loaded;
        Unloaded += OverviewPage_Unloaded;
        ActualThemeChanged += (_, _) => UpdateTheme();
        _networkActivityTimer.Tick += NetworkActivityTimer_Tick;
    }

    private void Dashboard_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var height = Math.Clamp(e.NewSize.Height - 590, 90, 210);
        PlotHeight.Height = new GridLength(height);
        BandwidthScaleUpperText.Margin = new Thickness(0, height / 4 - 7, 0, 0);
        BandwidthScaleLowerText.Margin = new Thickness(0, 0, 0, height / 4 - 7);
    }

    private void ProtectionSummary_Click(object sender, RoutedEventArgs e)
    {
        ProtectionPanel.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = _uiSettings.AnimationsEnabled });
    }

    private void ConnectionsSummary_Click(object sender, RoutedEventArgs e)
    {
        _mainWindow?.ShowConnections();
    }

    private void UpdateTheme()
    {
        var theme = _accessibilitySettings.HighContrast ? "HighContrast" : ActualTheme.ToString();
        var palette = (ResourceDictionary)Resources.ThemeDictionaries[theme];
        foreach (var option in ViewModel.ModeOptions)
        {
            var brush = option.Mode switch
            {
                ModernFirewallMode.Normal => "NetworkDownloadBrush",
                ModernFirewallMode.AllowOutgoing => "NetworkUploadBrush",
                ModernFirewallMode.BlockAll => "LocalUploadBrush",
                ModernFirewallMode.Learning => "LocalDownloadBrush",
                _ => "TotalUploadBrush"
            };
            option.ModeBrush.Color = ((SolidColorBrush)palette[brush]).Color;
        }
        UpdateNetworkActivityGraph();
    }

    private void SummaryPanels_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ArrangeColumns((Grid)sender, e.NewSize.Width >= 660 ? 3 : e.NewSize.Width >= 420 ? 2 : 1);
    }

    private void SeriesSelectors_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ArrangeColumns((Grid)sender, e.NewSize.Width >= 740 ? 3 : e.NewSize.Width >= 490 ? 2 : 1);
    }

    private static void ArrangeColumns(Grid panel, int columns)
    {
        if (panel.ColumnDefinitions.Count == columns)
            return;

        panel.ColumnDefinitions.Clear();
        panel.RowDefinitions.Clear();
        for (var column = 0; column < columns; column++)
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var row = 0; row < (panel.Children.Count + columns - 1) / columns; row++)
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var index = 0; index < panel.Children.Count; index++)
        {
            Grid.SetRow((FrameworkElement)panel.Children[index], index / columns);
            Grid.SetColumn((FrameworkElement)panel.Children[index], index % columns);
        }
    }

    private void ModeTile_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        AnimateModeTile((Button)sender, -3);
    }

    private void ModeTile_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        AnimateModeTile((Button)sender, 0);
    }

    private void AnimateModeTile(Button button, float offset)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(button, true);
        var visual = ElementCompositionPreview.GetElementVisual(button);
        if (!_uiSettings.AnimationsEnabled || _accessibilitySettings.HighContrast)
        {
            visual.StopAnimation("Translation.Y");
            visual.Properties.InsertVector3("Translation", Vector3.Zero);
            return;
        }

        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(1, offset);
        animation.Duration = TimeSpan.FromMilliseconds(180);
        visual.StartAnimation("Translation.Y", animation);
    }

    private async void OverviewPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isPageLoaded = true;
        UpdateTheme();
        _mainWindow = App.MainWindow as MainWindow;
        if (_mainWindow is not null)
        {
            _mainWindow.WindowVisibilityChanged += MainWindow_WindowVisibilityChanged;
            _isWindowVisible = _mainWindow.IsWindowVisible;
        }

        _isRestoringSeries = true;
        try
        {
            var options = await _optionsService.LoadAsync();
            var series = options.VisibleNetworkSeries;
            if ((series & ~NetworkActivitySeries.All) != 0)
                throw new InvalidOperationException("The saved traffic series selection is invalid.");
            InternetDownloadCheckBox.IsChecked = series.HasFlag(NetworkActivitySeries.InternetDownload);
            InternetUploadCheckBox.IsChecked = series.HasFlag(NetworkActivitySeries.InternetUpload);
            LocalDownloadCheckBox.IsChecked = series.HasFlag(NetworkActivitySeries.LocalDownload);
            LocalUploadCheckBox.IsChecked = series.HasFlag(NetworkActivitySeries.LocalUpload);
            TotalDownloadCheckBox.IsChecked = series.HasFlag(NetworkActivitySeries.TotalDownload);
            TotalUploadCheckBox.IsChecked = series.HasFlag(NetworkActivitySeries.TotalUpload);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not restore traffic series selection: {ex}");
            SeriesSettingsStatus.Text = $"Could not restore chart settings: {ex.Message}";
            SeriesSettingsStatus.Visibility = Visibility.Visible;
        }
        finally
        {
            _isRestoringSeries = false;
            _optionsReady = true;
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
        var shouldMonitor = _isPageLoaded && _isWindowVisible && _optionsReady;
        if (_isMonitoring == shouldMonitor)
            return;

        _isMonitoring = shouldMonitor;
        if (shouldMonitor)
        {
            ViewModel.ResetNetworkActivityBaseline();
            _lastSummaryRefresh = 0;
            _ = ViewModel.RefreshSummaryAsync();
            ViewModel.NetworkActivitySamples.CollectionChanged += NetworkActivitySamples_CollectionChanged;
            _networkActivityTimer.Start();
            UpdateNetworkActivityGraph();
        }
        else
        {
            _networkActivityTimer.Stop();
            ViewModel.ResetNetworkActivityBaseline();
            ViewModel.NetworkActivitySamples.CollectionChanged -= NetworkActivitySamples_CollectionChanged;
            NetworkActivityCanvas.Children.Clear();
            _outlines.Clear();
            _downloadPath.Data = null;
            _uploadPath.Data = null;
            _localDownloadPath.Data = null;
            _localUploadPath.Data = null;
            _totalDownloadPath.Data = null;
            _totalUploadPath.Data = null;
        }
    }

    private void NetworkActivitySamples_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateNetworkActivityGraph();
    }

    private async void NetworkActivityTimer_Tick(object? sender, object e)
    {
        if (_isMonitoring)
        {
            if (_lastSummaryRefresh == 0 || Stopwatch.GetElapsedTime(_lastSummaryRefresh).TotalSeconds >= 10)
            {
                _lastSummaryRefresh = Stopwatch.GetTimestamp();
                _ = ViewModel.RefreshSummaryAsync();
            }
            await ViewModel.UpdateNetworkActivityAsync();
        }
    }

    private void NetworkActivityCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateNetworkActivityGraph();
    }

    private void SeriesSelection_Changed(object sender, RoutedEventArgs e)
    {
        if (!_optionsReady || _isRestoringSeries || !_isPageLoaded)
            return;
        UpdateNetworkActivityGraph();
        var series = NetworkActivitySeries.None;
        if (InternetDownloadCheckBox.IsChecked == true) series |= NetworkActivitySeries.InternetDownload;
        if (InternetUploadCheckBox.IsChecked == true) series |= NetworkActivitySeries.InternetUpload;
        if (LocalDownloadCheckBox.IsChecked == true) series |= NetworkActivitySeries.LocalDownload;
        if (LocalUploadCheckBox.IsChecked == true) series |= NetworkActivitySeries.LocalUpload;
        if (TotalDownloadCheckBox.IsChecked == true) series |= NetworkActivitySeries.TotalDownload;
        if (TotalUploadCheckBox.IsChecked == true) series |= NetworkActivitySeries.TotalUpload;
        try
        {
            _optionsService.UpdateAsync(options => options with { VisibleNetworkSeries = series }).GetAwaiter().GetResult();
            SeriesSettingsStatus.Text = string.Empty;
            SeriesSettingsStatus.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not save traffic series selection: {ex}");
            SeriesSettingsStatus.Text = $"Could not save chart settings: {ex.Message}";
            SeriesSettingsStatus.Visibility = Visibility.Visible;
        }
    }

    private void UpdateNetworkActivityGraph()
    {
        if (!_isMonitoring || NetworkActivityCanvas.ActualWidth <= 0 || NetworkActivityCanvas.ActualHeight <= 0)
            return;

        var samples = ViewModel.NetworkActivitySamples;
        var classified = samples.Count == 0 || samples[samples.Count - 1].IsClassified;
        long maxValue = 2;

        foreach (var sample in samples)
        {
            if (InternetDownloadCheckBox.IsChecked == true)
                maxValue = Math.Max(maxValue, sample.ReceivedBytesPerSecond);
            if (InternetUploadCheckBox.IsChecked == true)
                maxValue = Math.Max(maxValue, sample.SentBytesPerSecond);
            if (LocalDownloadCheckBox.IsChecked == true)
                maxValue = Math.Max(maxValue, sample.LocalReceivedBytesPerSecond);
            if (LocalUploadCheckBox.IsChecked == true)
                maxValue = Math.Max(maxValue, sample.LocalSentBytesPerSecond);
            if (TotalDownloadCheckBox.IsChecked == true)
                maxValue = Math.Max(maxValue, sample.TotalReceivedBytesPerSecond);
            if (TotalUploadCheckBox.IsChecked == true)
                maxValue = Math.Max(maxValue, sample.TotalSentBytesPerSecond);
        }

        BandwidthScaleTopText.Text = FormatRate(maxValue);
        BandwidthScaleUpperText.Text = FormatRate((long)(maxValue * 0.75));
        BandwidthScaleLowerText.Text = FormatRate(maxValue / 4);
        BandwidthScaleMiddleText.Text = FormatRate(maxValue / 2);

        _totalDownloadPath.Visibility = TotalDownloadCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        _totalUploadPath.Visibility = TotalUploadCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        _downloadPath.Visibility = classified && InternetDownloadCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        _uploadPath.Visibility = classified && InternetUploadCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        _localDownloadPath.Visibility = classified && LocalDownloadCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        _localUploadPath.Visibility = classified && LocalUploadCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        var theme = _accessibilitySettings.HighContrast ? "HighContrast" : ActualTheme.ToString();
        var palette = (ResourceDictionary)Resources.ThemeDictionaries[theme];
        UpdateSeries(_totalDownloadPath, samples, maxValue, static sample => sample.TotalReceivedBytesPerSecond, (Brush)palette["TotalDownloadBrush"]);
        UpdateSeries(_totalUploadPath, samples, maxValue, static sample => sample.TotalSentBytesPerSecond, (Brush)palette["TotalUploadBrush"]);
        UpdateSeries(_downloadPath, samples, maxValue, static sample => sample.ReceivedBytesPerSecond, (Brush)palette["NetworkDownloadBrush"]);
        UpdateSeries(_uploadPath, samples, maxValue, static sample => sample.SentBytesPerSecond, (Brush)palette["NetworkUploadBrush"]);
        UpdateSeries(_localDownloadPath, samples, maxValue, static sample => sample.LocalReceivedBytesPerSecond, (Brush)palette["LocalDownloadBrush"]);
        UpdateSeries(_localUploadPath, samples, maxValue, static sample => sample.LocalSentBytesPerSecond, (Brush)palette["LocalUploadBrush"]);
    }

    private void UpdateSeries(Path path, IReadOnlyList<NetworkActivitySample> samples, long maxValue, Func<NetworkActivitySample, long> valueSelector, Brush brush)
    {
        if (path.Visibility == Visibility.Collapsed || samples.Count < 2)
        {
            path.Data = null;
            if (_outlines.TryGetValue(path, out var existingOutline))
                existingOutline.Data = null;
            return;
        }

        var width = NetworkActivityCanvas.ActualWidth;
        var height = NetworkActivityCanvas.ActualHeight;
        var xStep = width / 300;
        var latestTimestamp = samples[samples.Count - 1].Timestamp;
        var startX = width - (latestTimestamp - samples[0].Timestamp).TotalSeconds * xStep;
        var figure = new PathFigure
        {
            StartPoint = new(startX, height - (valueSelector(samples[0]) / (double)maxValue * height)),
            IsFilled = true,
            IsClosed = false
        };
        var previousPoint = figure.StartPoint;
        var outline = new PathFigure { StartPoint = figure.StartPoint, IsFilled = false };

        for (var i = 1; i < samples.Count; i++)
        {
            var x = width - (latestTimestamp - samples[i].Timestamp).TotalSeconds * xStep;
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
            outline.Segments.Add(new BezierSegment { Point1 = segment.Point1, Point2 = segment.Point2, Point3 = segment.Point3 });
            previousPoint = segment.Point3;
        }

        figure.Segments.Add(new LineSegment { Point = new(width, height) });
        figure.Segments.Add(new LineSegment { Point = new(startX, height) });
        figure.IsClosed = true;
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        path.Data = geometry;
        var colour = ((SolidColorBrush)brush).Color;
        var transparent = colour;
        transparent.A = 0;
        path.Fill = new LinearGradientBrush
        {
            StartPoint = new(0, 0),
            EndPoint = new(0, 1),
            Opacity = 0.3,
            GradientStops =
            {
                new GradientStop { Color = colour, Offset = 0 },
                new GradientStop { Color = transparent, Offset = 1 }
            }
        };
        if (!NetworkActivityCanvas.Children.Contains(path))
            NetworkActivityCanvas.Children.Add(path);
        if (!_outlines.TryGetValue(path, out var outlinePath))
        {
            outlinePath = new Path { StrokeThickness = 1.25 };
            Canvas.SetZIndex(outlinePath, 1);
            _outlines.Add(path, outlinePath);
            NetworkActivityCanvas.Children.Add(outlinePath);
        }
        var outlineGeometry = new PathGeometry();
        outlineGeometry.Figures.Add(outline);
        outlinePath.Data = outlineGeometry;
        outlinePath.Stroke = brush;
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
