using CommunityToolkit.Mvvm.ComponentModel;
using ModernTinyWall.TinyWall;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ModernTinyWall.ViewModels;

internal sealed class DataUsagePageViewModel : ObservableObject
{
    private readonly Func<DataUsagePeriod, Task<DataUsageSnapshot>> _readUsage;
    private ApplicationDataUsage[] _applications = [];
    private bool _isRefreshing;
    private bool _refreshRequested;
    private string _statusMessage = "Loading usage history...";
    private string _summary = string.Empty;
    private string _searchText = string.Empty;
    private int _sortIndex;
    private DataUsagePeriod _period;

    public DataUsagePageViewModel()
        : this(period => Task.Run(() => new Controller("TinyWallController").ReadDataUsage(period)))
    {
    }

    internal DataUsagePageViewModel(Func<DataUsagePeriod, Task<DataUsageSnapshot>> readUsage)
    {
        _readUsage = readUsage;
    }

    public ObservableCollection<DataUsageRow> Applications { get; } = [];

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetProperty(ref _isRefreshing, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                RebuildRows();
        }
    }

    public int SortIndex
    {
        get => _sortIndex;
        set
        {
            if (SetProperty(ref _sortIndex, value))
                RebuildRows();
        }
    }

    public DataUsagePeriod Period
    {
        get => _period;
        set => SetProperty(ref _period, value);
    }

    public async Task RefreshAsync()
    {
        _refreshRequested = true;
        if (IsRefreshing)
            return;
        IsRefreshing = true;
        try
        {
            do
            {
                _refreshRequested = false;
                var period = Period;
                try
                {
                    var snapshot = await _readUsage(period);
                    if (period != Period)
                    {
                        _refreshRequested = true;
                        continue;
                    }
                    _applications = snapshot.Applications;
                    RebuildRows();
                    var tracking = snapshot.TrackingStarted is { } started
                        ? $"Tracking since {started.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}. " +
                          $"Updated {DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture)}."
                        : "No traffic has been recorded yet.";
                    StatusMessage = snapshot.Error is { } error
                        ? $"{error} Displayed totals may be incomplete."
                        : tracking;
                    if (snapshot.Error is not null && _applications.Length == 0)
                        Summary = "Usage unavailable.";
                    if (!string.IsNullOrWhiteSpace(snapshot.Warning))
                        StatusMessage += $" {snapshot.Warning}";
                    if (_applications.Any(app => app.ApplicationPath == ApplicationDataUsage.UnattributedPath))
                        StatusMessage += " Unattributed traffic could not be matched to an executable.";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Could not load application data usage: {ex}");
                    _applications = [];
                    RebuildRows();
                    Summary = "Usage unavailable.";
                    StatusMessage = ex.Message;
                }
            } while (_refreshRequested);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void RebuildRows()
    {
        var matching = _applications.Where(app =>
            app.ApplicationPath.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase));
        var sorted = SortIndex switch
        {
            1 => matching.OrderBy(app => app.ApplicationPath, StringComparer.CurrentCultureIgnoreCase),
            2 => matching.OrderByDescending(app => app.InternetReceived),
            3 => matching.OrderByDescending(app => app.InternetSent),
            4 => matching.OrderByDescending(app => app.LocalReceived),
            5 => matching.OrderByDescending(app => app.LocalSent),
            _ => matching.OrderByDescending(app => app.Total)
        };
        Applications.Clear();
        long total = 0, internetReceived = 0, internetSent = 0, localReceived = 0, localSent = 0;
        foreach (var application in sorted)
        {
            Applications.Add(new DataUsageRow(application));
            total = checked(total + application.Total);
            internetReceived = checked(internetReceived + application.InternetReceived);
            internetSent = checked(internetSent + application.InternetSent);
            localReceived = checked(localReceived + application.LocalReceived);
            localSent = checked(localSent + application.LocalSent);
        }
        Summary = $"{Applications.Count} applications | Total {DataUsageRow.FormatBytes(total)} | " +
            $"Internet in {DataUsageRow.FormatBytes(internetReceived)}, out {DataUsageRow.FormatBytes(internetSent)} | " +
            $"Local in {DataUsageRow.FormatBytes(localReceived)}, out {DataUsageRow.FormatBytes(localSent)}";
    }
}

internal sealed class DataUsageRow(ApplicationDataUsage usage)
{
    public string Application => usage.ApplicationPath switch
    {
        ApplicationDataUsage.SystemPath => "System",
        ApplicationDataUsage.UnattributedPath => "Unattributed traffic",
        _ => Path.GetFileName(usage.ApplicationPath)
    };
    public string ApplicationPath => usage.ApplicationPath;
    public string Total => FormatBytes(usage.Total);
    public string InternetReceived => FormatBytes(usage.InternetReceived);
    public string InternetSent => FormatBytes(usage.InternetSent);
    public string LocalReceived => FormatBytes(usage.LocalReceived);
    public string LocalSent => FormatBytes(usage.LocalSent);

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB"];
        var value = (double)bytes;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }
        return $"{value.ToString(index == 0 ? "N0" : "N2", CultureInfo.CurrentCulture)} {units[index]}";
    }
}
