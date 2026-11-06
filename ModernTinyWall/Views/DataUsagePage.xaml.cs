using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.TinyWall;
using ModernTinyWall.ViewModels;
using System;

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

    private async void PeriodPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedIndex: >= 0 and <= 3 } picker)
            return;
        ViewModel.Period = (DataUsagePeriod)picker.SelectedIndex;
        if (_loaded)
            await ViewModel.RefreshAsync();
    }
}
