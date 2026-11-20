using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ModernTinyWall.Exceptions;
using ModernTinyWall.Models;
using ModernTinyWall.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ModernTinyWall.ViewModels;

internal sealed class ExceptionsPageViewModel : INotifyPropertyChanged
{
    private readonly ITinyWallExceptionStore _exceptionStore;
    private readonly IOptionsService _optionsService = new OptionsService();
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _clearCommand;
    private bool _isRefreshing;
    private string _statusMessage = "Ready";
    private string _searchText = string.Empty;
    private ExceptionRowViewModel? _selectedException;
    private IReadOnlyList<ExceptionRowViewModel> _allExceptions = [];
    private string _subjectTypeFilter = "All types";
    private string _policyFilter = "All policies";
    private int _ageDaysFilter;
    private string _healthSummary = "Load exceptions to review rule health.";
    private string _tagFilter = string.Empty;
    private IReadOnlyDictionary<Guid, string> _ruleTags = new Dictionary<Guid, string>();

    public ExceptionsPageViewModel()
        : this(App.Services.GetRequiredService<ITinyWallExceptionStore>())
    {
    }

    public async Task AddServiceExceptionAsync(string executablePath, string serviceName)
    {
        IsRefreshing = true;
        try
        {
            var result = await _exceptionStore.AddServiceExceptionAsync(executablePath, serviceName);
            StatusMessage = result.Message;
            if (result.Success)
                await RefreshAsync(SearchText);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public async Task AddPackageExceptionAsync(string packageSid, string displayName, string publisherId, string publisher)
    {
        IsRefreshing = true;
        try
        {
            var result = await _exceptionStore.AddPackageExceptionAsync(packageSid, displayName, publisherId, publisher);
            StatusMessage = result.Message;
            if (result.Success)
                await RefreshAsync(SearchText);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public async Task AddExecutableExceptionsAsync(string executablePath)
    {
        IsRefreshing = true;
        try
        {
            var result = await _exceptionStore.AddExecutableExceptionsAsync(executablePath);
            StatusMessage = result.Message;
            if (result.Success)
                await RefreshAsync(SearchText);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    internal ExceptionsPageViewModel(ITinyWallExceptionStore exceptionStore)
    {
        ArgumentNullException.ThrowIfNull(exceptionStore);

        _exceptionStore = exceptionStore;
        _refreshCommand = new AsyncRelayCommand(RefreshAsync, CanRunCommand);
        _clearCommand = new AsyncRelayCommand(ClearAsync, CanRunCommand);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ExceptionRowViewModel> Exceptions { get; } = [];

    public IAsyncRelayCommand RefreshCommand => _refreshCommand;

    public IAsyncRelayCommand ClearCommand => _clearCommand;

    public string SearchText
    {
        get => _searchText;
        set => SetField(ref _searchText, value);
    }

    public ExceptionRowViewModel? SelectedException
    {
        get => _selectedException;
        set => SetField(ref _selectedException, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetField(ref _isRefreshing, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public string HealthSummary
    {
        get => _healthSummary;
        private set => SetField(ref _healthSummary, value);
    }

    public void SetFilters(string subjectType, string policy, int ageDays)
    {
        _subjectTypeFilter = subjectType;
        _policyFilter = policy;
        _ageDaysFilter = ageDays;
        ApplyFilters();
    }

    public void SetTagFilter(string tag)
    {
        _tagFilter = tag.Trim();
        ApplyFilters();
    }

    public async Task SetTagsAsync(Guid id, string tags)
    {
        var normalised = string.Join(", ", tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase));
        if (normalised.Length > 200)
        {
            StatusMessage = "Rule tags must be 200 characters or fewer.";
            return;
        }

        try
        {
            await _optionsService.UpdateAsync(options =>
            {
                var updated = new Dictionary<Guid, string>(options.RuleTags);
                if (normalised.Length == 0)
                    updated.Remove(id);
                else
                    updated[id] = normalised;
                return options with { RuleTags = updated };
            });
            _ruleTags = (await _optionsService.LoadAsync()).RuleTags;
            ApplyFilters();
            StatusMessage = "Rule tags saved on this PC.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save rule tags: {ex.Message}";
        }
    }

    public async Task RefreshAsync(string searchText)
    {
        SearchText = searchText;
        IsRefreshing = true;
        StatusMessage = "Refreshing application exceptions...";

        try
        {
            var rows = await _exceptionStore.GetExceptionsAsync(new ExceptionQuery(searchText));
            _ruleTags = (await _optionsService.LoadAsync()).RuleTags;
            _allExceptions = rows.Select(ExceptionRowViewModel.FromModel).ToArray();
            SelectedException = null;
            ApplyFilters();
        }
        catch (Exception ex)
        {
            _allExceptions = [];
            Exceptions.Clear();
            HealthSummary = "Rule health is unavailable until exceptions can be read.";
            StatusMessage = $"Could not refresh application exceptions: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void ApplyFilters()
    {
        var cutoff = _ageDaysFilter == 0 ? (DateTime?)null : DateTime.Now.AddDays(-_ageDaysFilter);
        var duplicateSubjects = _allExceptions
            .GroupBy(row => $"{row.SubjectType}\0{row.Details}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var reviewed = _allExceptions.Select(row => row with
        {
            Tags = _ruleTags.TryGetValue(row.Id, out var tags) ? tags : string.Empty,
            ReviewFinding = ExceptionHealthAnalyzer.GetFinding(
                new ExceptionRow(row.Id, row.Name, row.SubjectType, row.Details, row.Policy, row.Created),
                duplicateSubjects.Contains($"{row.SubjectType}\0{row.Details}"))
        }).ToArray();
        var findingsCount = reviewed.Count(row => row.ReviewFinding.Length > 0);
        HealthSummary = ExceptionHealthAnalyzer.CreateSummary(findingsCount);

        var matching = reviewed.Where(row =>
            (_subjectTypeFilter == "All types" || row.SubjectType == _subjectTypeFilter) &&
            MatchesPolicy(row.Policy, _policyFilter) &&
            (_tagFilter.Length == 0 || row.Tags.Contains(_tagFilter, StringComparison.CurrentCultureIgnoreCase)) &&
            (cutoff is null || (DateTime.TryParseExact(row.Created, "yyyy/MM/dd HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var created) && created >= cutoff)));

        SelectedException = null;
        Exceptions.Clear();
        foreach (var row in matching)
            Exceptions.Add(row);

        StatusMessage = $"{Exceptions.Count} of {_allExceptions.Count} exception{(_allExceptions.Count == 1 ? string.Empty : "s")}";
    }

    private static bool MatchesPolicy(string policy, string filter) => filter switch
    {
        "All policies" => true,
        "Hard block" => policy == "Hard block",
        "Unrestricted" => policy.StartsWith("Unrestricted", StringComparison.OrdinalIgnoreCase),
        "TCP/UDP" => policy.StartsWith("TCP", StringComparison.OrdinalIgnoreCase)
            || policy.StartsWith("UDP", StringComparison.OrdinalIgnoreCase),
        "Rule list" => policy.StartsWith("Rule list", StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    private Task RefreshAsync()
    {
        return RefreshAsync(SearchText);
    }

    private Task ClearAsync()
    {
        SearchText = string.Empty;
        return RefreshAsync(SearchText);
    }

    private bool CanRunCommand()
    {
        return !IsRefreshing;
    }

    public async Task AddExceptionAsync(string subjectType, string name, string details, string policy, string remoteTcpPorts = "", string localTcpPorts = "", string remoteUdpPorts = "", string localUdpPorts = "")
    {
        IsRefreshing = true;
        try
        {
            var result = await _exceptionStore.AddExceptionAsync(new ExceptionEditRequest(subjectType, name, details, policy, remoteTcpPorts, localTcpPorts, remoteUdpPorts, localUdpPorts));
            StatusMessage = result.Message;
            if (result.Success)
                await RefreshAsync(SearchText);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public async Task ModifySelectedAsync(string subjectType, string name, string details, string policy, string remoteTcpPorts = "", string localTcpPorts = "", string remoteUdpPorts = "", string localUdpPorts = "")
    {
        if (SelectedException is null)
        {
            StatusMessage = "Select an exception to modify.";
            return;
        }

        IsRefreshing = true;
        try
        {
            var result = await _exceptionStore.UpdateExceptionAsync(SelectedException.Id, new ExceptionEditRequest(subjectType, name, details, policy, remoteTcpPorts, localTcpPorts, remoteUdpPorts, localUdpPorts));
            StatusMessage = result.Message;
            if (result.Success)
                await RefreshAsync(SearchText);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public async Task RemoveSelectedAsync(IReadOnlyCollection<Guid> ids)
    {
        IsRefreshing = true;
        try
        {
            var result = await _exceptionStore.RemoveExceptionsAsync(ids);
            if (result.Success)
                await RefreshAsync(SearchText);
            StatusMessage = result.Message;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public async Task RemoveAllAsync()
    {
        IsRefreshing = true;
        try
        {
            var result = await _exceptionStore.RemoveAllExceptionsAsync();
            StatusMessage = result.Message;
            if (result.Success)
                await RefreshAsync(SearchText);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsRefreshing))
        {
            _refreshCommand.NotifyCanExecuteChanged();
            _clearCommand.NotifyCanExecuteChanged();
        }
    }
}

internal sealed record ExceptionRowViewModel(Guid Id, string Name, string SubjectType, string Details, string Policy, string Created, string IconPath, string PackageFamilyName, string Duration, string ReviewFinding = "", string Tags = "")
{
    public string PolicyScope
    {
        get
        {
            var scope = Policy switch
            {
                "Hard block" => "Blocks inbound and outbound traffic.",
                "Unrestricted, local network only" => "Allows traffic only to and from the local network.",
                var value when value.StartsWith("Unrestricted", StringComparison.OrdinalIgnoreCase) =>
                    "Allows unrestricted traffic for this subject, subject to other firewall rules.",
                var value when value.StartsWith("TCP", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("UDP", StringComparison.OrdinalIgnoreCase) =>
                    "Allows the configured TCP/UDP ports and directions. Select Modify to review the port lists.",
                var value when value.StartsWith("Rule list", StringComparison.OrdinalIgnoreCase) =>
                    "Applies the explicit rules in this rule list.",
                _ => "The traffic scope depends on this policy's configured rules."
            };
            return $"Applies to {SubjectType}: {Name}. {scope}";
        }
    }

    public static ExceptionRowViewModel FromModel(ExceptionRow row)
    {
        return new ExceptionRowViewModel(row.Id, row.Name, row.SubjectType, row.Details, row.Policy, row.Created, row.IconPath, row.PackageFamilyName, row.Duration);
    }
}
