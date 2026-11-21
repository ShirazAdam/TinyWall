using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed partial class ApplicationFileDialogue : ContentDialog
{
    private ApplicationFileDialogue()
    {
        InitializeComponent();
    }

    internal static async Task ShowAsync(XamlRoot xamlRoot, IReadOnlyList<ApplicationFileTarget> targets)
    {
        var files = ApplicationFileInformation.DistinctFiles(targets);
        var dialog = new ApplicationFileDialogue
        {
            XamlRoot = xamlRoot,
            RequestedTheme = xamlRoot.Content is FrameworkElement root ? root.ActualTheme : ElementTheme.Default
        };
        dialog.LayoutRoot.Width = Math.Min(640, Math.Max(200, xamlRoot.Size.Width - 120));
        dialog.DetailsScroll.MaxHeight = Math.Max(120, Math.Min(560, xamlRoot.Size.Height - 320));
        if (files.Count > 1)
        {
            foreach (var file in files)
                dialog.FilePicker.Items.Add(file.DisplayName);
            dialog.FilePicker.SelectedIndex = 0;
            dialog.FilePicker.Visibility = Visibility.Visible;
        }

        var closed = false;
        var revision = 0;
        var completion = new TaskCompletionSource();
        dialog.Closed += (_, _) => { closed = true; revision++; };

        async Task LoadAsync()
        {
            var current = ++revision;
            var target = files[Math.Max(0, dialog.FilePicker.SelectedIndex)];
            dialog.FileTitle.Text = target.Name;
            dialog.FileSubtitle.Text = "Read-only application information";
            dialog.FileIcon.SourcePath = target.FilePath;
            dialog.LoadingPanel.Visibility = Visibility.Visible;
            dialog.GeneralCard.Visibility = dialog.VersionCard.Visibility =
                dialog.SignatureCard.Visibility = Visibility.Collapsed;
            dialog.FileError.IsOpen = false;
            var information = await Task.Run(() => ApplicationFileInformation.Read(target));
            if (closed || current != revision)
                return;

            dialog.LoadingPanel.Visibility = Visibility.Collapsed;
            dialog.FileError.Message = information.Error ?? "";
            dialog.FileError.IsOpen = information.Error is not null;
            dialog.GeneralFields.Children.Clear();
            dialog.VersionFields.Children.Clear();
            var fields = information.Fields.ToDictionary(field => field.Key, field => field.Value);
            if (fields.TryGetValue("File name", out var name))
                dialog.FileTitle.Text = name;
            if (fields.TryGetValue("Description", out var description) && description != "Unavailable")
                dialog.FileSubtitle.Text = description;

            foreach (var field in information.Fields)
            {
                if (field.Key is "Digitally signed (embedded signature)" or "File name")
                    continue;
                var panel = field.Key is "Description" or "Product name" or "File version" or "Product version"
                    or "Original file name" or "Copyright" ? dialog.VersionFields : dialog.GeneralFields;
                dialog.AddField(panel, field.Key, field.Value);
            }
            dialog.GeneralCard.Visibility = Visibility.Visible;
            dialog.VersionCard.Visibility = dialog.VersionFields.Children.Count > 0
                ? Visibility.Visible : Visibility.Collapsed;
            dialog.SignatureSummary.Text = information.Signature switch
            {
                WinTrust.VerifyResult.SIGNATURE_VALID => "Embedded signature verified",
                WinTrust.VerifyResult.SIGNATURE_MISSING => "No embedded signature",
                WinTrust.VerifyResult.SIGNATURE_INVALID => "Signature not verified as trusted",
                _ => "Signature information unavailable"
            };
            dialog.SignatureIcon.Glyph = information.Signature == WinTrust.VerifyResult.SIGNATURE_VALID
                ? "\uE73E" : "\uE946";
            dialog.SignatureDetails.Text = fields.GetValueOrDefault("Digitally signed (embedded signature)",
                "This row does not identify an accessible executable file.");
            dialog.SignatureCard.Visibility = Visibility.Visible;
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

        dialog.FilePicker.SelectionChanged += async (_, _) => await LoadSafelyAsync();
        dialog.Opened += async (_, _) => await LoadSafelyAsync();
        var operation = dialog.ShowAsync();
        operation.Completed = (result, _) =>
        {
            try { result.GetResults(); completion.TrySetResult(); }
            catch (Exception ex) { completion.TrySetException(ex); }
        };
        await completion.Task;
    }

    private void AddField(StackPanel panel, string label, string value)
    {
        var row = new Grid { ColumnSpacing = 16 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(new TextBlock { Text = label, Style = (Style)Resources["DetailLabelStyle"] });
        var text = new TextBlock { Text = value, Style = (Style)Resources["DetailValueStyle"] };
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        panel.Children.Add(row);
    }
}
