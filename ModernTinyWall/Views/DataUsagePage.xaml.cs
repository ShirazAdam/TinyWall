using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.Services;
using ModernTinyWall.Models;
using ModernTinyWall.TinyWall;
using ModernTinyWall.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Text;
using WinRT.Interop;

namespace ModernTinyWall.Views;

public sealed partial class DataUsagePage : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _loaded;
    private bool _windowVisible = true;
    private MainWindow? _mainWindow;
    internal DataUsagePageViewModel ViewModel { get; } = new();

    public DataUsagePage()
    {
        InitializeComponent();
        UsageTable.Configure("dataUsage",
            TableColumn.Text<DataUsageRow>("name", "Application", 300, row => row.Application),
            TableColumn.Value<DataUsageRow, long>("total", "Total", 140, row => row.TotalBytes),
            TableColumn.Value<DataUsageRow, long>("internetReceived", "Internet incoming", 170, row => row.InternetReceivedBytes),
            TableColumn.Value<DataUsageRow, long>("internetSent", "Internet outgoing", 170, row => row.InternetSentBytes),
            TableColumn.Value<DataUsageRow, long>("localReceived", "Local incoming", 160, row => row.LocalReceivedBytes),
            TableColumn.Value<DataUsageRow, long>("localSent", "Local outgoing", 160, row => row.LocalSentBytes));
        Loaded += Page_Loaded;
        Unloaded += Page_Unloaded;
        _timer.Tick += Timer_Tick;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        _mainWindow = App.MainWindow as MainWindow;
        if (_mainWindow is not null)
        {
            _mainWindow.WindowVisibilityChanged += MainWindow_WindowVisibilityChanged;
            _windowVisible = _mainWindow.IsWindowVisible;
        }

        UpdateRefreshState();
        if (_windowVisible)
            await ViewModel.RefreshAsync();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _loaded = false;
        if (_mainWindow is not null)
        {
            _mainWindow.WindowVisibilityChanged -= MainWindow_WindowVisibilityChanged;
            _mainWindow = null;
        }

        UpdateRefreshState();
    }

    private async void MainWindow_WindowVisibilityChanged(bool isVisible)
    {
        _windowVisible = isVisible;
        UpdateRefreshState();
        if (_loaded && isVisible && !ViewModel.IsRefreshing)
            await ViewModel.RefreshAsync();
    }

    private void UpdateRefreshState()
    {
        if (_loaded && _windowVisible)
            _timer.Start();
        else
            _timer.Stop();
    }

    private async void Timer_Tick(object? sender, object e)
    {
        if (_loaded && _windowVisible && !ViewModel.IsRefreshing)
            await ViewModel.RefreshAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var path = FilePickerService.PickSaveCsvFile(
            WindowNative.GetWindowHandle(App.MainWindow), "Export data usage", "Modern-TinyWall-data-usage.csv");
        if (path is null)
            return;

        try
        {
            await File.WriteAllTextAsync(path, ViewModel.CreateCsv(UsageTable.OrderedItems.OfType<DataUsageRow>()), new UTF8Encoding(false));
            ViewModel.SetExportStatus($"Exported filtered data usage to {path}.");
        }
        catch (Exception ex)
        {
            ViewModel.SetExportStatus($"Could not export data usage: {ex.Message}");
        }
    }

    private async void PeriodPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedIndex: >= 0 and <= 3 } picker)
            return;
        ViewModel.Period = (DataUsagePeriod)picker.SelectedIndex;
        if (_loaded)
            await ViewModel.RefreshAsync();
    }
}
