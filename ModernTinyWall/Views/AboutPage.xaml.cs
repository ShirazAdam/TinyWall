using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;

namespace ModernTinyWall.Views;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
    }

    private void LicenceButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        OpenFirstExistingPath(
            Path.Combine(AppContext.BaseDirectory, "Licence.rtf"),
            Path.Combine(AppContext.BaseDirectory, "License.rtf"));
    }

    private void AttributionsButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        OpenPath(Path.Combine(AppContext.BaseDirectory, "Attributions.txt"));
    }

    private void GitHubButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        OpenPath("https://github.com/ShirazAdam/tinywall");
    }

    private static void OpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
            // Best-effort external link/file opening.
        }
    }

    private static void OpenFirstExistingPath(params string[] paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                OpenPath(path);
                return;
            }
        }

        if (paths.Length > 0)
            OpenPath(paths[0]);
    }
}
