using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal static class ApplicationFileDialogue
{
    internal static async Task ShowAsync(XamlRoot xamlRoot, IReadOnlyList<ApplicationFileTarget> targets)
    {
        var files = ApplicationFileInformation.DistinctFiles(targets);
        var content = new StackPanel { Spacing = 12 };
        var details = new StackPanel { Spacing = 8 };
        ComboBox? picker = null;
        if (files.Count > 1)
        {
            content.Children.Add(new TextBlock
            {
                Text = "This group contains different files. Select a file to inspect its own information.",
                TextWrapping = TextWrapping.Wrap
            });
            picker = new ComboBox
            {
                Header = "Application file",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = files,
                DisplayMemberPath = nameof(ApplicationFileTarget.DisplayName),
                SelectedIndex = 0
            };
            AutomationProperties.SetName(picker, "Application file");
            content.Children.Add(picker);
        }
        content.Children.Add(details);
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Additional information",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            Content = new ScrollViewer
            {
                Content = content,
                MaxHeight = Math.Max(120, Math.Min(520, xamlRoot.Size.Height - 200)),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            }
        };
        var closed = false;
        var revision = 0;
        var completion = new TaskCompletionSource();
        dialog.Closed += (_, _) => { closed = true; revision++; };

        async Task LoadAsync()
        {
            var current = ++revision;
            details.Children.Clear();
            details.Children.Add(new ProgressRing { IsActive = true, Width = 24, Height = 24 });
            var target = picker?.SelectedItem as ApplicationFileTarget ?? files[0];
            var information = await Task.Run(() => ApplicationFileInformation.Read(target));
            if (closed || current != revision)
                return;
            details.Children.Clear();
            if (information.Error is { } error)
                details.Children.Add(new InfoBar
                {
                    IsOpen = true, IsClosable = false, Severity = InfoBarSeverity.Warning,
                    Title = "File information unavailable", Message = error
                });
            foreach (var field in information.Fields)
            {
                details.Children.Add(new TextBlock
                {
                    Text = field.Key, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                });
                details.Children.Add(new TextBlock
                {
                    Text = field.Value, IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap
                });
            }
            details.Children.Add(new TextBlock
            {
                Text = "Details describe the file currently on disk, not necessarily the image loaded by a running process. Signature checks use cached revocation data and do not guarantee safety. Catalogue-only signatures are not checked.",
                TextWrapping = TextWrapping.Wrap
            });
        }

        async Task LoadSafelyAsync()
        {
            try { await LoadAsync(); }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
                dialog.Hide();
            }
        }

        if (picker is not null)
            picker.SelectionChanged += async (_, _) => await LoadSafelyAsync();
        dialog.Opened += async (_, _) => await LoadSafelyAsync();
        var operation = dialog.ShowAsync();
        operation.Completed = (result, _) =>
        {
            try { result.GetResults(); completion.TrySetResult(); }
            catch (Exception ex) { completion.TrySetException(ex); }
        };
        await completion.Task;
    }
}
