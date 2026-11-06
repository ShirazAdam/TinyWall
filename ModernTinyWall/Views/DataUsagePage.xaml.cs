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
        _timer.Start();
        await ViewModel.RefreshAsync();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _loaded = false;
        _timer.Stop();
    }

    private async void Timer_Tick(object? sender, object e)
    {
        if (!ViewModel.IsRefreshing)
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
