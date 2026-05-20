using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed class DialogService : IDialogService
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
}
