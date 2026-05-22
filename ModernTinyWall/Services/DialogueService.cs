using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed class DialogueService : IDialogueService
{
    public Task ShowMessageAsync(XamlRoot xamlRoot, string title, string message, string closeButtonText = "OK")
    {
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = closeButtonText
        };

        _ = dialog.ShowAsync();
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmAsync(XamlRoot xamlRoot, string title, string message, string primaryButtonText = "OK", string closeButtonText = "Cancel")
    {
        var completion = new TaskCompletionSource<bool>();
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText
        };

        var operation = dialog.ShowAsync();
        operation.Completed = (asyncInfo, _) =>
        {
            try
            {
                completion.TrySetResult(asyncInfo.GetResults() == ContentDialogResult.Primary);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        };

        return completion.Task;
    }
}
