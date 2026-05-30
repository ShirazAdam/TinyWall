using CommunityToolkit.Mvvm.Input;
using ModernTinyWall.Exceptions;
using ModernTinyWall.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ModernTinyWall.ViewModels;

internal sealed class ExceptionsPageViewModel : INotifyPropertyChanged
{
    private readonly IExceptionsService _exceptionsService;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _clearCommand;
    private bool _isRefreshing;
    private string _statusMessage = "Ready";
    private string _searchText = string.Empty;
    private ExceptionRowViewModel? _selectedException;

    public ExceptionsPageViewModel()
        : this(new ExceptionsService())
    {
    }

    public async Task AddServiceExceptionAsync(string executablePath, string serviceName)
    {
        IsRefreshing = true;
        try
        {
            var result = await _exceptionsService.AddServiceExceptionAsync(executablePath, serviceName);
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
            var result = await _exceptionsService.AddPackageExceptionAsync(packageSid, displayName, publisherId, publisher);
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
            var result = await _exceptionsService.AddExecutableExceptionsAsync(executablePath);
            StatusMessage = result.Message;
            if (result.Success)
                await RefreshAsync(SearchText);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    internal ExceptionsPageViewModel(IExceptionsService exceptionsService)
    {
        _exceptionsService = exceptionsService;
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

    public async Task RefreshAsync(string searchText)
    {
        SearchText = searchText;
        IsRefreshing = true;
        StatusMessage = "Refreshing application exceptions...";

        try
        {
            var rows = await _exceptionsService.GetExceptionsAsync(new ExceptionQuery(searchText));
            Exceptions.Clear();
            SelectedException = null;
            foreach (var row in rows)
            {
                Exceptions.Add(ExceptionRowViewModel.FromModel(row));
            }

            StatusMessage = $"{Exceptions.Count} exception{(Exceptions.Count == 1 ? string.Empty : "s")}";
        }
        catch (Exception ex)
        {
            Exceptions.Clear();
            StatusMessage = $"Could not refresh application exceptions: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

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
            var result = await _exceptionsService.AddExceptionAsync(new ExceptionEditRequest(subjectType, name, details, policy, remoteTcpPorts, localTcpPorts, remoteUdpPorts, localUdpPorts));
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
            var result = await _exceptionsService.UpdateExceptionAsync(SelectedException.Id, new ExceptionEditRequest(subjectType, name, details, policy, remoteTcpPorts, localTcpPorts, remoteUdpPorts, localUdpPorts));
            StatusMessage = result.Message;
            if (result.Success)
                await RefreshAsync(SearchText);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public async Task RemoveSelectedAsync()
    {
        if (SelectedException is null)
        {
            StatusMessage = "Select an exception to remove.";
            return;
        }

        IsRefreshing = true;
        try
        {
            var result = await _exceptionsService.RemoveExceptionAsync(SelectedException.Id);
            StatusMessage = result.Message;
            if (result.Success)
                await RefreshAsync(SearchText);
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
            var result = await _exceptionsService.RemoveAllExceptionsAsync();
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

internal sealed record ExceptionRowViewModel(Guid Id, string Name, string SubjectType, string Details, string Policy, string Created)
{
    public static ExceptionRowViewModel FromModel(ExceptionRow row)
    {
        return new ExceptionRowViewModel(row.Id, row.Name, row.SubjectType, row.Details, row.Policy, row.Created);
    }
}
