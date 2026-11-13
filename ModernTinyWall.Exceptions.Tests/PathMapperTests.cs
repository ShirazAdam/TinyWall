using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.TinyWall;
using ModernTinyWall.ViewModels;
using ModernTinyWall.Windows;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class PathMapperTests
{
    [TestMethod]
    public void VolumeEnumerationDoesNotDependOnConsumersLastNativeError()
    {
        var count = 0;
        foreach (var volume in FindVolumeSafeHandle.EnumerateVolumes())
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(volume));
            count++;
            Marshal.SetLastPInvokeError(0);
        }
        Assert.IsGreaterThan(0, count);
    }

    [TestMethod]
    public void ConvertsNativeSystemVolumePathToDrivePath()
    {
        var (nativePath, expected) = GetSystemExecutablePaths();
        Assert.AreEqual(expected, PathMapper.Instance.ConvertPath(nativePath.AsSpan(), PathFormat.Win32), ignoreCase: true);
    }

    [TestMethod]
    public void FirewallLogPathsAreConvertedWithoutInventingUnknownDriveMappings()
    {
        var (nativePath, expected) = GetSystemExecutablePaths();
        const string unknown = @"\Device\TinyWallUnknownVolume\app.exe";
        var entries = Controller.EndReadFwLog(new TwMessageReadFwLog([
            new FirewallLogEntry { AppPath = nativePath },
            new FirewallLogEntry { AppPath = unknown }
        ]));
        Assert.AreEqual(expected, entries[0].AppPath, ignoreCase: true);
        Assert.AreEqual(unknown, entries[1].AppPath);
    }

    [TestMethod]
    public async Task HistoricalUsageDisplaysAndSearchesByDrivePathWithoutChangingHistory()
    {
        var (nativePath, expected) = GetSystemExecutablePaths();
        var snapshot = new DataUsageSnapshot([new(nativePath, 1, 0, 0, 0)], null, null, null);
        var viewModel = new DataUsagePageViewModel(_ => Task.FromResult(snapshot));
        await viewModel.RefreshAsync();
        viewModel.SearchText = Environment.SystemDirectory;
        Assert.HasCount(1, viewModel.Applications);
        Assert.AreEqual(expected, viewModel.Applications[0].ApplicationPath, ignoreCase: true);
        Assert.AreEqual(nativePath, snapshot.Applications[0].ApplicationPath);
    }

    [TestMethod]
    public void ExceptionDetailsShowDrivePathWithoutChangingStoredSubject()
    {
        var (nativePath, expected) = GetSystemExecutablePaths();
        var subject = new ExecutableSubject(nativePath);
        var details = ExceptionDescriptor.DescribeSubject(subject);
        Assert.AreEqual(expected, details.Details, ignoreCase: true);
        Assert.AreEqual(nativePath, subject.ExecutablePath);
    }

    [TestMethod]
    public void ConvertsSystemRootToWindowsDirectory()
    {
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "svchost.exe");
        Assert.AreEqual(expected, PathMapper.Instance.ConvertPath(@"\SystemRoot\System32\svchost.exe", PathFormat.Win32), ignoreCase: true);
    }

    private static (string Native, string Win32) GetSystemExecutablePaths()
    {
        var systemDirectory = Environment.SystemDirectory;
        var root = Path.GetPathRoot(systemDirectory)!;
        var target = new StringBuilder(1024);
        Assert.IsGreaterThan(0, QueryDosDevice(root[..2], target, target.Capacity));
        return (target + systemDirectory[(root.Length - 1)..] + @"\svchost.exe",
            Path.Combine(systemDirectory, "svchost.exe"));
    }

    [DllImport("kernel32.dll", EntryPoint = "QueryDosDeviceW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int QueryDosDevice(string device, StringBuilder target, int length);
}
