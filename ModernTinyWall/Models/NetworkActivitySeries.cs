using System;

namespace ModernTinyWall.Models;

[Flags]
public enum NetworkActivitySeries
{
    None = 0,
    InternetDownload = 1,
    InternetUpload = 2,
    LocalDownload = 4,
    LocalUpload = 8,
    TotalDownload = 16,
    TotalUpload = 32,
    All = InternetDownload | InternetUpload | LocalDownload | LocalUpload | TotalDownload | TotalUpload
}
