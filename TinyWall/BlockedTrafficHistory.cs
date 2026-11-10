using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

namespace ModernTinyWall.TinyWall;

internal sealed class BlockedTrafficHistory : IDisposable
{
    private readonly object _gate = new();
    private readonly string _connectionString;
    private readonly Action<Exception> _logError;
    private readonly Dictionary<DateOnly, long> _pending = [];
    private Timer? _timer;
    private string? _error = "Blocked traffic collection has not started.";
    private string? _eventError;
    private bool _started;
    private bool _disposed;

    public BlockedTrafficHistory(string path, Action<Exception> logError)
    {
        _logError = logError;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(path),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();
    }

    public void Start(DateTimeOffset now, bool automaticFlush = true)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started)
                throw new InvalidOperationException("Blocked traffic collection has already started.");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(new SqliteConnectionStringBuilder(_connectionString).DataSource)!);
                using var connection = Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    PRAGMA journal_mode = WAL;
                    PRAGMA synchronous = FULL;
                    CREATE TABLE IF NOT EXISTS blocked_days (
                        day TEXT PRIMARY KEY,
                        count INTEGER NOT NULL CHECK(count >= 0)
                    ) STRICT;
                    CREATE TABLE IF NOT EXISTS metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL) STRICT;
                    INSERT OR REPLACE INTO metadata(key, value)
                        SELECT 'warning', 'Collection was interrupted. The last uncommitted events may be missing.'
                        WHERE EXISTS (SELECT 1 FROM metadata WHERE key = 'active' AND value = '1');
                    INSERT OR IGNORE INTO metadata(key, value) VALUES ('started', $started);
                    INSERT OR REPLACE INTO metadata(key, value) VALUES ('active', '1');
                    """;
                command.Parameters.AddWithValue("$started", now.ToString("O", CultureInfo.InvariantCulture));
                command.ExecuteNonQuery();
                _started = true;
                _error = null;
                if (automaticFlush)
                    _timer = new Timer(_ => FlushInBackground(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                ReportFailure(ex);
            }
        }
    }

    public void Record(FirewallLogEntry entry)
    {
        // Count WFP drop notifications, not unique sockets, packets or application attempts.
        if (entry.Event != EventLogEvent.BLOCKED)
            return;
        lock (_gate)
        {
            if (!_started || _disposed)
                return;
            try
            {
                var day = DateOnly.FromDateTime(entry.Timestamp.ToLocalTime());
                _pending.TryGetValue(day, out var count);
                _pending[day] = checked(count + 1);
            }
            catch (Exception ex)
            {
                ReportFailure(ex);
                _eventError = _error;
            }
        }
    }

    public BlockedTrafficSnapshot Query(DateOnly today)
    {
        lock (_gate)
        {
            if (!_started || _disposed)
                return new(today, 0, null, _disposed ? "Blocked traffic collection has stopped." : _error, null);
            try
            {
                Flush();
                using var connection = Open();
                using var transaction = connection.BeginTransaction();
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "SELECT count FROM blocked_days WHERE day = $day;";
                command.Parameters.AddWithValue("$day", today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                var count = command.ExecuteScalar() switch
                {
                    null => 0L,
                    long value when value >= 0 => value,
                    _ => throw new InvalidDataException("Blocked traffic history contains an invalid count.")
                };
                command.Parameters.Clear();
                command.CommandText = "SELECT value FROM metadata WHERE key = 'started';";
                var started = DateTimeOffset.Parse((string)command.ExecuteScalar()!, CultureInfo.InvariantCulture);
                command.CommandText = "SELECT value FROM metadata WHERE key = 'warning';";
                var interrupted = command.ExecuteScalar() as string;
                transaction.Commit();
                _error = _eventError;
                var warning = "Observed WFP drop events only, not unique connections. Only periods when the service is collecting are included; earlier events and service downtime are not recovered.";
                if (interrupted is not null)
                    warning += " " + interrupted;
                return new(today, count, started, _error, warning);
            }
            catch (Exception ex)
            {
                ReportFailure(ex);
                return new(today, 0, null, _error, null);
            }
        }
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private void Flush()
    {
        if (_pending.Count == 0)
            return;
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO blocked_days(day, count) VALUES ($day, $count)
            ON CONFLICT(day) DO UPDATE SET count = count + excluded.count;
            """;
        var day = command.Parameters.Add("$day", SqliteType.Text);
        var count = command.Parameters.Add("$count", SqliteType.Integer);
        foreach (var delta in _pending)
        {
            day.Value = delta.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            count.Value = delta.Value;
            command.ExecuteNonQuery();
        }
        transaction.Commit();
        _pending.Clear();
    }

    private void FlushInBackground()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            try { Flush(); }
            catch (Exception ex) { ReportFailure(ex); }
        }
    }

    private void ReportFailure(Exception ex)
    {
        _error = $"Blocked traffic history is unavailable: {ex.Message}";
        _logError(ex);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _timer?.Dispose();
            if (!_started)
                return;
            try
            {
                Flush();
                using var connection = Open();
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT OR REPLACE INTO metadata(key, value) VALUES ('active', '0');";
                command.ExecuteNonQuery();
            }
            catch (Exception ex) { ReportFailure(ex); }
        }
    }
}
