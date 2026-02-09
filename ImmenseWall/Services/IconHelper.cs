using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImmenseWall.Services
{
    public static class IconHelper
    {
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        public static ImageSource? ToImageSource(System.Drawing.Icon icon)
        {
            if (icon == null) return null;

            using (System.Drawing.Bitmap bitmap = icon.ToBitmap())
            {
                IntPtr hBitmap = bitmap.GetHbitmap();

                try
                {
                    ImageSource wpfBitmap = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    return wpfBitmap;
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
        }

        public static ImageSource? GetIcon(string path)
        {
            try
            {
                using (var icon = IconTools.GetIconForFile(path, IconTools.ShellIconSize.SmallIcon))
                {
                    if (icon == null) return null;
                    return ToImageSource(icon);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
