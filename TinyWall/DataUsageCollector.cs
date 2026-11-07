using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using ModernTinyWall.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;

namespace ModernTinyWall.TinyWall;

internal sealed class DataUsageCollector : IDisposable
{
    private const string IncompleteWarning = "Some network events were lost. Recorded history may be incomplete.";
    private readonly object _gate = new();
    private readonly object _sessionGate = new();
    private readonly Dictionary<int, string> _applications = [];
    private readonly Dictionary<(DateOnly Day, string Application), DataUsageDelta> _pending = new(new DailyApplicationComparer());
    private readonly Action<Exception> _logError;
    private readonly string _historyPath;
    private readonly string _sessionName;
    private readonly Thread _worker;
    private DataUsageHistory? _history;
    private TraceEventSession? _session;
    private string? _error = "Starting data usage collection.";
    private volatile bool _stopping;
    private volatile bool _failed;

    public DataUsageCollector(string historyPath, Action<Exception> logError, string sessionName = "TinyWall.DataUsage")
    {
        _historyPath = historyPath;
        _logError = logError;
        _sessionName = sessionName;
        _worker = new Thread(Run) { IsBackground = true, Name = "TinyWall Data Usage" };
        _worker.Start();
    }

    public DataUsageSnapshot Query(DataUsagePeriod period)
    {
        if (!Enum.IsDefined(period))
            throw new ArgumentOutOfRangeException(nameof(period));
        try
        {
            lock (_gate)
            {
                if (_history is null)
                    return new([], null, _error, null);
                Flush();
                return _history.Query(period, DateOnly.FromDateTime(DateTime.Now), _error);
            }
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
            StopProcessing();
            lock (_gate)
                return new([], null, _error, null);
        }
    }

    private void Run()
    {
        Timer? flushTimer = null;
        TraceEventSession? session = null;
        var collectionStarted = false;
        try
        {
            lock (_gate)
                _history = new DataUsageHistory(_historyPath);

            session = new TraceEventSession(_sessionName)
            {
                StopOnDispose = true,
                BufferSizeMB = 32
            };
            lock (_sessionGate)
            {
                if (_stopping || _failed)
                    return;
                session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP | KernelTraceEventParser.Keywords.Process);
                var kernel = session.Source.Kernel;
                kernel.ProcessStartGroup += ProcessStarted;
                kernel.ProcessStop += data => _applications.Remove(data.ProcessID);
                kernel.TcpIpSend += data => Record(data, data.size, data.daddr, true);
                kernel.TcpIpRecv += data => Record(data, data.size, data.saddr, false);
                kernel.TcpIpSendIPV6 += data => Record(data, data.size, data.daddr, true);
                kernel.TcpIpRecvIPV6 += data => Record(data, data.size, data.saddr, false);
                kernel.UdpIpSend += data => Record(data, data.size, data.daddr, true);
                kernel.UdpIpRecv += data => Record(data, data.size, data.saddr, false);
                kernel.UdpIpSendIPV6 += data => Record(data, data.size, data.daddr, true);
                kernel.UdpIpRecvIPV6 += data => Record(data, data.size, data.saddr, false);
                _session = session;
            }
            lock (_gate)
            {
                _history.MarkTrackingStarted(DateTimeOffset.Now);
                collectionStarted = true;
                _error = null;
            }
            flushTimer = new Timer(_ => FlushInBackground(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            if (!_stopping)
                session.Source.Process();
            if (!_stopping && !_failed)
                throw new InvalidOperationException("The network trace session ended unexpectedly.");
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }
        finally
        {
            if (flushTimer is not null)
            {
                using var finished = new ManualResetEvent(false);
                flushTimer.Dispose(finished);
                finished.WaitOne();
            }
            try
            {
                lock (_gate)
                {
                    Flush();
                    if (_stopping && collectionStarted)
                        _history?.MarkTrackingStopped();
                }
            }
            catch (Exception ex)
            {
                ReportFailure(ex);
            }
            try
            {
                lock (_sessionGate)
                {
                    _session = null;
                    session?.Dispose();
                }
            }
            catch (Exception ex)
            {
                ReportFailure(ex);
            }
        }
    }

    private void ProcessStarted(ProcessTraceData data)
    {
        var path = ResolveApplication(data.ProcessID);
        // A PID can be reused before a queued start event is consumed.
        if (path != ApplicationDataUsage.SystemPath && path != ApplicationDataUsage.UnattributedPath &&
            !string.Equals(Path.GetFileName(path), Path.GetFileName(data.ImageFileName), StringComparison.OrdinalIgnoreCase))
            path = ApplicationDataUsage.UnattributedPath;
        _applications[data.ProcessID] = path;
    }

    private static string ResolveApplication(int processId)
    {
        if (processId is 0 or 4)
            return ApplicationDataUsage.SystemPath;
        if (processId < 0)
            return ApplicationDataUsage.UnattributedPath;

        var path = ProcessManager.GetProcessPath(unchecked((uint)processId));
        return string.IsNullOrWhiteSpace(path) ? ApplicationDataUsage.UnattributedPath : path;
    }

    private void Record(TraceEvent data, int size, IPAddress remoteAddress, bool outgoing)
    {
        if (size <= 0)
            return;
        if (!_applications.TryGetValue(data.ProcessID, out var application))
        {
            application = ResolveApplication(data.ProcessID);
            _applications[data.ProcessID] = application;
        }
        AddUsage(DateOnly.FromDateTime(data.TimeStamp), application, size, outgoing,
            DataUsageAddressClassifier.IsLocal(remoteAddress));
    }

    private void AddUsage(DateOnly day, string application, long bytes, bool outgoing, bool local)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(application);
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        if (bytes == 0)
            return;
        lock (_gate)
        {
            var key = (day, application);
            if (!_pending.TryGetValue(key, out var existing))
                existing = new(day, application, 0, 0, 0, 0);
            _pending[key] = existing.Add(bytes, outgoing, local);
        }
    }

    private void Flush()
    {
        if (_history is null || _pending.Count == 0)
            return;
        _history.Append(_pending.Values);
        _pending.Clear();
    }

    private void FlushInBackground()
    {
        try
        {
            bool eventsLost;
            lock (_sessionGate)
                eventsLost = !_stopping && _session is not null && _session.EventsLost > 0;
            lock (_gate)
            {
                if (eventsLost)
                    _history?.MarkIncomplete(IncompleteWarning);
                Flush();
            }
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
            StopProcessing();
        }
    }

    private void ReportFailure(Exception exception)
    {
        lock (_gate)
        {
            if (!_failed)
                _error = $"Data usage collection failed: {exception.Message}";
            _failed = true;
            try
            {
                _history?.MarkIncomplete("Data usage collection failed during this history. Recorded totals may be incomplete.");
            }
            catch (Exception historyException)
            {
                _logError(historyException);
            }
        }
        _logError(exception);
    }

    public void Dispose()
    {
        _stopping = true;
        try
        {
            lock (_sessionGate)
            {
                if (_session is not null && _session.EventsLost > 0)
                {
                    lock (_gate)
                        _history?.MarkIncomplete(IncompleteWarning);
                }
                _session?.Stop();
            }
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
            StopProcessing();
        }
        if (!_worker.Join(TimeSpan.FromSeconds(10)))
            ReportFailure(new TimeoutException("The data usage collector did not finish flushing before shutdown."));
    }

    private void StopProcessing()
    {
        lock (_sessionGate)
            _session?.Source.StopProcessing();
    }

    private sealed class DailyApplicationComparer : IEqualityComparer<(DateOnly Day, string Application)>
    {
        public bool Equals((DateOnly Day, string Application) x, (DateOnly Day, string Application) y) =>
            x.Day == y.Day && StringComparer.OrdinalIgnoreCase.Equals(x.Application, y.Application);

        public int GetHashCode((DateOnly Day, string Application) value) =>
            HashCode.Combine(value.Day, StringComparer.OrdinalIgnoreCase.GetHashCode(value.Application));
    }
}
