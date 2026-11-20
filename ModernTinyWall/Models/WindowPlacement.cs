using System;
using global::Windows.Graphics;

namespace ModernTinyWall.Models;

internal static class WindowPlacement
{
    internal const int MinimumSavedWidth = 480;
    internal const int MinimumSavedHeight = 360;
    internal const int MaximumSavedDimension = 32_768;

    internal static readonly SizeInt32 DefaultSize = new(1536, 1024);

    internal static bool IsValidSavedSize(int? width, int? height) =>
        width is >= MinimumSavedWidth and <= MaximumSavedDimension
        && height is >= MinimumSavedHeight and <= MaximumSavedDimension;

    internal static SizeInt32 ResolvePreferredSize(int? savedWidth, int? savedHeight) =>
        IsValidSavedSize(savedWidth, savedHeight)
            ? new SizeInt32(savedWidth!.Value, savedHeight!.Value)
            : DefaultSize;

    internal static RectInt32 Centre(RectInt32 workArea, SizeInt32 preferred)
    {
        if (workArea.Width <= 0 || workArea.Height <= 0 || preferred.Width <= 0 || preferred.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(workArea), "Window and work-area dimensions must be positive.");
        var width = Math.Min(preferred.Width, Math.Max(1, workArea.Width - 32));
        var height = Math.Min(preferred.Height, Math.Max(1, workArea.Height - 32));
        return new RectInt32(
            workArea.X + (workArea.Width - width) / 2,
            workArea.Y + (workArea.Height - height) / 2,
            width, height);
    }
}
