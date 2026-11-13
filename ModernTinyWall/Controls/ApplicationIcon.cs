using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using ModernTinyWall.Services;
using System;
using System.Diagnostics;

namespace ModernTinyWall.Controls;

public sealed class ApplicationIcon : Grid
{
    public static readonly DependencyProperty SourcePathProperty = DependencyProperty.Register(
        nameof(SourcePath), typeof(string), typeof(ApplicationIcon), new PropertyMetadata(string.Empty, OnSourceChanged));
    public static readonly DependencyProperty PackageFamilyNameProperty = DependencyProperty.Register(
        nameof(PackageFamilyName), typeof(string), typeof(ApplicationIcon), new PropertyMetadata(string.Empty, OnSourceChanged));

    private readonly FontIcon _fallback = new() { Glyph = "\uE737", FontSize = 17 };
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private int _generation;

    public string SourcePath
    {
        get => (string)GetValue(SourcePathProperty);
        set => SetValue(SourcePathProperty, value);
    }

    public string PackageFamilyName
    {
        get => (string)GetValue(PackageFamilyNameProperty);
        set => SetValue(PackageFamilyNameProperty, value);
    }

    public ApplicationIcon()
    {
        Width = 24;
        Height = 24;
        VerticalAlignment = VerticalAlignment.Center;
        IsHitTestVisible = false;
        Children.Add(_fallback);
        Children.Add(_image);
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) => _generation++;
    }

    private static void OnSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var icon = (ApplicationIcon)sender;
        if (icon.IsLoaded)
            icon.Refresh();
    }

    private async void Refresh()
    {
        var generation = ++_generation;
        _image.Source = null;
        _fallback.Visibility = Visibility.Visible;
        try
        {
            BitmapImage? source = await ApplicationIconLoader.GetAsync(SourcePath, PackageFamilyName);
            if (generation == _generation && IsLoaded && source is not null)
            {
                _image.Source = source;
                _fallback.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not load application icon: {ex}");
        }
    }
}
