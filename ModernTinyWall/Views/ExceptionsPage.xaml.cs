using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.Services;
using ModernTinyWall.ViewModels;
using System;
using System.Threading.Tasks;
using WinRT.Interop;

namespace ModernTinyWall.Views;

public sealed partial class ExceptionsPage : Page
{
    private readonly IDialogueService _dialogueService = new DialogueService();
    internal ExceptionsPageViewModel ViewModel { get; } = new();

    public ExceptionsPage()
    {
        InitializeComponent();
        Loaded += ExceptionsPage_Loaded;
    }

    private async void ExceptionsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        await RefreshAsync();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SearchText = SearchBox.Text;
    }

    private void ExceptionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedException = (sender as ListView)?.SelectedItem as ExceptionRowViewModel;
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var sourceType = await ShowAddSourceDialogAsync();
        if (sourceType == "Executable")
        {
            var executablePath = FilePickerService.PickOpenFile(WindowNative.GetWindowHandle(App.MainWindow), "Choose executable");
            if (executablePath is not null)
            {
                await ViewModel.AddExecutableExceptionsAsync(executablePath);
                return;
            }
        }
        else if (sourceType == "Service")
        {
            var serviceRequest = await ShowServiceExceptionDialogAsync();
            if (serviceRequest is not null)
            {
                await ViewModel.AddServiceExceptionAsync(serviceRequest.Value.ExecutablePath, serviceRequest.Value.ServiceName);
                return;
            }
        }
        else if (sourceType == "Package")
        {
            var packageRequest = await ShowPackageExceptionDialogAsync();
            if (packageRequest is not null)
            {
                await ViewModel.AddPackageExceptionAsync(packageRequest.Value.PackageSid, packageRequest.Value.DisplayName, packageRequest.Value.PublisherId, packageRequest.Value.Publisher);
                return;
            }
        }

        var request = await ShowExceptionEditorAsync("Add exception");
        if (request is not null)
            await ViewModel.AddExceptionAsync(request.Value.SubjectType, request.Value.Name, request.Value.Details, request.Value.Policy);
    }

    private async void ModifyButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedException is null)
        {
            await _dialogueService.ShowMessageAsync(XamlRoot, "Modify exception", "Select an exception to modify.");
            return;
        }

        var request = await ShowExceptionEditorAsync("Modify exception", ViewModel.SelectedException);
        if (request is not null)
            await ViewModel.ModifySelectedAsync(request.Value.SubjectType, request.Value.Name, request.Value.Details, request.Value.Policy);
    }

    private async void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedException is null)
        {
            await _dialogueService.ShowMessageAsync(XamlRoot, "Remove exception", "Select an exception to remove.");
            return;
        }

        if (await _dialogueService.ConfirmAsync(XamlRoot, "Remove exception", "Remove the selected application exception?", "Remove"))
            await ViewModel.RemoveSelectedAsync();
    }

    private async void RemoveAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (await _dialogueService.ConfirmAsync(XamlRoot, "Remove all exceptions", "Remove all application exceptions?", "Remove all"))
            await ViewModel.RemoveAllAsync();
    }

    private Task RefreshAsync()
    {
        return ViewModel.RefreshAsync(SearchBox.Text);
    }

    private async Task<ExceptionEditorRequest?> ShowExceptionEditorAsync(string title, ExceptionRowViewModel? existing = null)
    {
        var subjectType = new ComboBox
        {
            Header = "Type",
            ItemsSource = new[] { "Executable", "Service", "Global", "UWP app" },
            SelectedItem = existing?.SubjectType ?? "Executable"
        };
        var nameBox = new TextBox { Header = "Name", Text = existing?.Name ?? string.Empty };
        var detailsBox = new TextBox { Header = "Details", Text = existing?.Details ?? string.Empty };
        var policyBox = new ComboBox
        {
            Header = "Policy",
            ItemsSource = new[] { "Unrestricted", "Hard block", "TCP/UDP" },
            SelectedItem = existing?.Policy.Contains("Hard block") == true ? "Hard block" : existing?.Policy.Contains("TCP") == true ? "TCP/UDP" : "Unrestricted"
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(subjectType);
        panel.Children.Add(nameBox);
        panel.Children.Add(detailsBox);
        panel.Children.Add(policyBox);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel"
        };

        var completion = new TaskCompletionSource<ContentDialogResult>();
        var operation = dialog.ShowAsync();
        operation.Completed = (asyncInfo, _) =>
        {
            try
            {
                completion.TrySetResult(asyncInfo.GetResults());
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        };

        if (await completion.Task != ContentDialogResult.Primary)
            return null;

        return new ExceptionEditorRequest(
            subjectType.SelectedItem as string ?? "Executable",
            nameBox.Text,
            detailsBox.Text,
            policyBox.SelectedItem as string ?? "Unrestricted");
    }

    private async Task<string> ShowAddSourceDialogAsync()
    {
        var sourceType = new ComboBox
        {
            Header = "Source",
            ItemsSource = new[] { "Executable", "Service", "Package", "Manual" },
            SelectedItem = "Executable"
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Add exception",
            Content = sourceType,
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel"
        };

        return await ShowDialogAsync(dialog) == ContentDialogResult.Primary
            ? sourceType.SelectedItem as string ?? "Manual"
            : "Manual";
    }

    private async Task<ServiceExceptionRequest?> ShowServiceExceptionDialogAsync()
    {
        var serviceNameBox = new TextBox { Header = "Service name" };
        var executablePathBox = new TextBox { Header = "Executable path" };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(serviceNameBox);
        panel.Children.Add(executablePathBox);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Add service exception",
            Content = panel,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel"
        };

        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
            return null;

        return new ServiceExceptionRequest(executablePathBox.Text, serviceNameBox.Text);
    }

    private async Task<PackageExceptionRequest?> ShowPackageExceptionDialogAsync()
    {
        var packageSidBox = new TextBox { Header = "Package SID" };
        var displayNameBox = new TextBox { Header = "Display name" };
        var publisherIdBox = new TextBox { Header = "Publisher ID" };
        var publisherBox = new TextBox { Header = "Publisher" };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(packageSidBox);
        panel.Children.Add(displayNameBox);
        panel.Children.Add(publisherIdBox);
        panel.Children.Add(publisherBox);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Add package exception",
            Content = panel,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel"
        };

        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
            return null;

        return new PackageExceptionRequest(packageSidBox.Text, displayNameBox.Text, publisherIdBox.Text, publisherBox.Text);
    }

    private static Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        var completion = new TaskCompletionSource<ContentDialogResult>();
        var operation = dialog.ShowAsync();
        operation.Completed = (asyncInfo, _) =>
        {
            try
            {
                completion.TrySetResult(asyncInfo.GetResults());
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        };

        return completion.Task;
    }

    private readonly record struct ExceptionEditorRequest(string SubjectType, string Name, string Details, string Policy);
    private readonly record struct ServiceExceptionRequest(string ExecutablePath, string ServiceName);
    private readonly record struct PackageExceptionRequest(string PackageSid, string DisplayName, string PublisherId, string Publisher);
}
