using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ModernTinyWall.TinyWall;

internal sealed partial class PendingConnectionDriver : IDisposable
{
    internal static readonly Guid CalloutV4 = new("7b788b57-6009-4dfe-9af0-216690b96b71");
    internal static readonly Guid CalloutV6 = new("d6ff3c55-1924-4361-b535-c76a44337764");
    private const uint ReadIoctl = 0x80006000;
    private const uint DecideIoctl = 0x8000A004;
    private const uint QueryIoctl = 0x80006008;
    private const int Version = 2;
    private const int RecordSize = 2096;
    private const int MaximumRequests = 128;
    private readonly SafeFileHandle _handle;

    internal PendingConnectionDriver()
    {
        _handle = CreateFile(@"\\.\TinyWallPending", 0xC0000000, 0, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (_handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            _handle.Dispose();
            throw new Win32Exception(error, "Pending connection prompts require the separately installed TinyWallPending driver.");
        }
    }

    internal unsafe FirewallLogEntry[] ReadPendingConnections()
    {
        var buffer = new byte[8 + MaximumRequests * RecordSize];
        uint bytesRead;
        fixed (byte* output = buffer)
        {
            if (!DeviceIoControl(_handle, ReadIoctl, null, 0, output, (uint)buffer.Length, out bytesRead, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (bytesRead != buffer.Length || BinaryPrimitives.ReadUInt32LittleEndian(buffer) != Version)
            throw new InvalidOperationException("Unsupported pending connection driver protocol.");
        var count = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(4));
        if (count > MaximumRequests)
            throw new InvalidOperationException("Invalid pending connection count from driver.");
        var entries = new FirewallLogEntry[count];
        for (var i = 0; i < entries.Length; ++i)
        {
            var record = buffer.AsSpan(8 + i * RecordSize, RecordSize);
            var family = BinaryPrimitives.ReadUInt32LittleEndian(record[20..]);
            if (family is not (2 or 23))
                throw new InvalidOperationException("Unsupported pending connection address family.");
            var nativePath = Encoding.Unicode.GetString(record.Slice(48, 2048)).TrimEnd('\0');
            var pid = BinaryPrimitives.ReadUInt64LittleEndian(record[8..]);
            if (pid > uint.MaxValue)
                throw new InvalidOperationException("Invalid process ID from driver.");
            entries[i] = new FirewallLogEntry
            {
                PendingConnectionId = BinaryPrimitives.ReadUInt64LittleEndian(record),
                ProcessId = (uint)pid,
                Timestamp = DateTime.Now,
                Event = EventLogEvent.BLOCKED,
                Direction = RuleDirection.Out,
                Protocol = (Protocol)BinaryPrimitives.ReadUInt32LittleEndian(record[16..]),
                LocalPort = BinaryPrimitives.ReadUInt16LittleEndian(record[24..]),
                RemotePort = BinaryPrimitives.ReadUInt16LittleEndian(record[26..]),
                RemoteIp = new IPAddress(record.Slice(32, family == 2 ? 4 : 16)).ToString(),
                AppPath = ModernTinyWall.Windows.PathMapper.Instance.ConvertPathIgnoreErrors(nativePath, ModernTinyWall.Windows.PathFormat.Win32)
            };
        }
        return entries;
    }

    internal unsafe void Resolve(ulong connectionId, bool allow)
    {
        Span<byte> decision = stackalloc byte[16];
        decision.Clear();
        BinaryPrimitives.WriteUInt64LittleEndian(decision, connectionId);
        BinaryPrimitives.WriteUInt32LittleEndian(decision[8..], allow ? 1u : 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(decision[12..], Version);
        fixed (byte* input = decision)
        {
            if (!DeviceIoControl(_handle, DecideIoctl, input, 16, null, 0, out _, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The connection decision could not be applied; it may have timed out.");
        }

        Span<byte> query = stackalloc byte[16];
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(5))
        {
            query.Clear();
            BinaryPrimitives.WriteUInt64LittleEndian(query, connectionId);
            BinaryPrimitives.WriteUInt32LittleEndian(query[8..], Version);
            fixed (byte* input = query)
            {
                if (!DeviceIoControl(_handle, QueryIoctl, input, 16, input, 16, out var bytesRead, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not confirm the pending connection decision.");
                if (bytesRead != 16 || BinaryPrimitives.ReadUInt64LittleEndian(query) != connectionId
                    || BinaryPrimitives.ReadUInt32LittleEndian(query[8..]) != Version)
                    throw new InvalidOperationException("Invalid pending connection decision response.");
            }
            switch (BinaryPrimitives.ReadUInt32LittleEndian(query[12..]))
            {
                case 0:
                    Thread.Sleep(50);
                    break;
                case 1:
                    return;
                case 2:
                    throw new InvalidOperationException("The pending connection decision could not be completed.");
                default:
                    throw new InvalidOperationException("Unsupported pending connection decision status.");
            }
        }
        throw new TimeoutException("The connection decision could not be confirmed; the application may need to retry.");
    }

    public void Dispose() => _handle.Dispose();

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool DeviceIoControl(SafeFileHandle handle, uint code, byte* input, uint inputLength, byte* output, uint outputLength, out uint bytesReturned, IntPtr overlapped);
}
