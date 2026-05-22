using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal interface IDialogService
{
    Task ShowMessageAsync(XamlRoot xamlRoot, string title, string message, string closeButtonText = "OK");
    Task<bool> ConfirmAsync(XamlRoot xamlRoot, string title, string message, string primaryButtonText = "OK", string closeButtonText = "Cancel");
}
