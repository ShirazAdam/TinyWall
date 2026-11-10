using System;
using System.Globalization;

namespace ModernTinyWall.Models;

internal enum PreviewTheme { System, Light, Dark }

internal sealed record UiPreviewOptions(PreviewTheme Theme = PreviewTheme.System, bool ReducedMotion = false,
    int? Width = null, int? Height = null)
{
    internal bool AnimationsAllowed(bool systemAnimations, bool highContrast) =>
        systemAnimations && !highContrast && !ReducedMotion;

    internal static UiPreviewOptions Parse(string[] arguments)
    {
        var options = new UiPreviewOptions();
        foreach (var argument in arguments)
        {
            if (argument == "--preview-reduced-motion")
                options = options with { ReducedMotion = true };
            else if (argument.StartsWith("--preview-theme=", StringComparison.Ordinal))
                options = options with
                {
                    Theme = argument["--preview-theme=".Length..] switch
                    {
                        "system" => PreviewTheme.System,
                        "light" => PreviewTheme.Light,
                        "dark" => PreviewTheme.Dark,
                        _ => throw new ArgumentException("Preview theme must be system, light or dark.")
                    }
                };
            else if (argument.StartsWith("--preview-size=", StringComparison.Ordinal))
            {
                var dimensions = argument["--preview-size=".Length..].Split('x');
                if (dimensions.Length != 2 ||
                    !int.TryParse(dimensions[0], NumberStyles.None, CultureInfo.InvariantCulture, out var width) ||
                    !int.TryParse(dimensions[1], NumberStyles.None, CultureInfo.InvariantCulture, out var height) ||
                    width is < 640 or > 4096 || height is < 480 or > 4096)
                    throw new ArgumentException("Preview size must be WIDTHxHEIGHT, from 640x480 to 4096x4096 pixels.");
                options = options with { Width = width, Height = height };
            }
            else if (argument.StartsWith("--preview-", StringComparison.Ordinal))
                throw new ArgumentException($"Unknown UI preview option: {argument}");
        }
        return options;
    }
}
