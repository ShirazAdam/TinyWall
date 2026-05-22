using ModernTinyWall.TinyWall;
using ModernTinyWall.Windows.NetStat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed class ConnectionsService : IConnectionsService
{
    private readonly Controller _controller = new("TinyWallController");

    public Task<IReadOnlyList<ConnectionRow>> GetConnectionsAsync(ConnectionQuery query, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<ConnectionRow>>(() => BuildConnections(query, cancellationToken), cancellationToken);
    }

    private List<ConnectionRow> BuildConnections(ConnectionQuery query, CancellationToken cancellationToken)
    {
        var rows = new List<ConnectionRow>();
        AddTcpRows(rows, NetStat.GetExtendedTcp4Table(false), query, cancellationToken);
        AddTcpRows(rows, NetStat.GetExtendedTcp6Table(false), query, cancellationToken);

        if (query.ShowListening)
        {
            var dummyRemote = new IPEndPoint(0, 0);
            AddUdpRows(rows, NetStat.GetExtendedUdp4Table(false), dummyRemote, cancellationToken);
            AddUdpRows(rows, NetStat.GetExtendedUdp6Table(false), dummyRemote, cancellationToken);
        }

        if (query.ShowBlocked)
        {
            AddBlockedRows(rows, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            rows = [.. rows.Where(row => row.Application.Contains(query.SearchText, StringComparison.CurrentCultureIgnoreCase))];
        }

        return rows;
    }

    private static void AddTcpRows(List<ConnectionRow> rows, TcpTable table, ConnectionQuery query, CancellationToken cancellationToken)
    {
        foreach (TcpRow tcpRow in table)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if ((!query.ShowListening || (tcpRow.State != TcpState.Listen))
                && (!query.ShowActive || (tcpRow.State == TcpState.Listen)))
                continue;

            rows.Add(CreateRow(tcpRow.ProcessId, "TCP", tcpRow.LocalEndPoint, tcpRow.RemoteEndPoint, tcpRow.State.ToString(), string.Empty));
        }
    }

    private static void AddUdpRows(List<ConnectionRow> rows, UdpTable table, IPEndPoint dummyRemote, CancellationToken cancellationToken)
    {
        foreach (UdpRow udpRow in table)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows.Add(CreateRow(udpRow.ProcessId, "UDP", udpRow.LocalEndPoint, dummyRemote, "Listen", string.Empty));
        }
    }

    private void AddBlockedRows(List<ConnectionRow> rows, CancellationToken cancellationToken)
    {
        try
        {
            var request = _controller.BeginReadFwLog();
            var fwLog = Controller.EndReadFwLog(request.Response);
            var now = DateTime.Now;
            var recentBlockedEntries = CoalesceRecentBlockedEntries(fwLog, now);

            foreach (var entry in recentBlockedEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry is not { LocalIp: not null, RemoteIp: not null })
                    continue;

                rows.Add(new ConnectionRow(
                    GetBlockedApplication(entry),
                    entry.Protocol.ToString(),
                    entry.LocalPort.ToString().PadLeft(5),
                    entry.LocalIp,
                    entry.RemotePort.ToString().PadLeft(5),
                    entry.RemoteIp,
                    "Blocked",
                    DirectionToString(entry.Direction)));
            }
        }
        catch
        {
            // The TinyWall service may be unavailable; active/listening data remains useful without blocked log data.
        }
    }

    private static List<FirewallLogEntry> CoalesceRecentBlockedEntries(FirewallLogEntry[] fwLog, DateTime now)
    {
        var filteredLog = new List<FirewallLogEntry>();
        var refSpan = TimeSpan.FromMinutes(5);

        foreach (var newEntry in fwLog)
        {
            if (now - newEntry.Timestamp > refSpan)
                continue;

            switch (newEntry.Event)
            {
                case EventLogEvent.ALLOWED_LISTEN:
                case EventLogEvent.ALLOWED_CONNECTION:
                case EventLogEvent.ALLOWED_LOCAL_BIND:
                case EventLogEvent.ALLOWED:
                    continue;
                case EventLogEvent.BLOCKED_LISTEN:
                case EventLogEvent.BLOCKED_CONNECTION:
                case EventLogEvent.BLOCKED_LOCAL_BIND:
                case EventLogEvent.BLOCKED_PACKET:
                case EventLogEvent.BLOCKED:
                    newEntry.Event = EventLogEvent.BLOCKED;
                    var existing = filteredLog.FirstOrDefault(oldEntry => oldEntry.Equals(newEntry, false));
                    if (existing is not null)
                    {
                        existing.Timestamp = newEntry.Timestamp;
                    }
                    else
                    {
                        filteredLog.Add(newEntry);
                    }
                    break;
            }
        }

        return filteredLog;
    }

    private static ConnectionRow CreateRow(uint processId, string protocol, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string state, string direction)
    {
        var application = GetProcessName(processId);
        return new ConnectionRow(
            application,
            protocol,
            localEndPoint.Port.ToString().PadLeft(5),
            localEndPoint.Address.ToString(),
            remoteEndPoint.Port.ToString().PadLeft(5),
            remoteEndPoint.Address.ToString(),
            state,
            direction);
    }

    private static string GetBlockedApplication(FirewallLogEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.AppPath))
            return entry.AppPath;

        if (!string.IsNullOrWhiteSpace(entry.PackageId))
            return entry.PackageId;

        return GetProcessName(entry.ProcessId);
    }

    private static string DirectionToString(RuleDirection direction)
    {
        return direction switch
        {
            RuleDirection.In => "In",
            RuleDirection.Out => "Out",
            RuleDirection.InOut => "In/Out",
            _ => string.Empty
        };
    }

    private static string GetProcessName(uint processId)
    {
        if (processId == 0)
            return "System";

        try
        {
            using var process = Process.GetProcessById(unchecked((int)processId));
            return $"{process.ProcessName} ({processId})";
        }
        catch
        {
            return processId.ToString();
        }
    }
}
