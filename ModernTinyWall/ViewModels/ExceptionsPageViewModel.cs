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
    private bool _isRefreshing;
    private string _statusMessage = "Ready";
    private ExceptionRowViewModel? _selectedException;

    public ExceptionsPageViewModel()
        : this(new ExceptionsService())
    {
    }

    internal ExceptionsPageViewModel(IExceptionsService exceptionsService)
    {
        _exceptionsService = exceptionsService;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ExceptionRowViewModel> Exceptions { get; } = [];
    public string SearchText { get; set; } = string.Empty;

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

    public async Task AddExceptionAsync(string subjectType, string name, string details, string policy)
    {
        IsRefreshing = true;
        try
        {
            var result = await _exceptionsService.AddExceptionAsync(new ExceptionEditRequest(subjectType, name, details, policy));
            StatusMessage = result.Message;
            if (result.Success)
                await RefreshAsync(SearchText);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public async Task ModifySelectedAsync(string subjectType, string name, string details, string policy)
    {
        if (SelectedException is null)
        {
            StatusMessage = "Select an exception to modify.";
            return;
        }

        IsRefreshing = true;
        try
        {
            var result = await _exceptionsService.UpdateExceptionAsync(SelectedException.Id, new ExceptionEditRequest(subjectType, name, details, policy));
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
    }
}

internal sealed record ExceptionRowViewModel(Guid Id, string Name, string SubjectType, string Details, string Policy, string Created)
{
    public static ExceptionRowViewModel FromModel(ExceptionRow row)
    {
        return new ExceptionRowViewModel(row.Id, row.Name, row.SubjectType, row.Details, row.Policy, row.Created);
    }
}
