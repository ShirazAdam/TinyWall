using System;
using global::Windows.Graphics;

namespace ModernTinyWall.Models;

internal static class WindowPlacement
{
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
