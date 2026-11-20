using Microsoft.Data.Sqlite;
using ModernTinyWall.TinyWall;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed class ConnectionHistoryService
{
    private const int MaximumEntries = 5000;
    private static readonly SemaphoreSlim FileLock = new(1, 1);
    private static readonly Lazy<bool> SqliteProvider = new(() =>
    {
        SQLitePCL.Batteries_V2.Init();
        return true;
    });
    private readonly IConnectionsService _connectionsService;
    private readonly string _historyPath;
    private readonly object _monitorLock = new();
    private CancellationTokenSource? _monitorCancellation;
    private bool _isEnabled;
    private bool _isWindowVisible;
    private bool _schemaInitialized;
    private int _retentionDays = 30;

    public ConnectionHistoryService(IConnectionsService connectionsService)
        : this(connectionsService, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ModernTinyWall", "connection-history.db"))
    {
    }

    internal ConnectionHistoryService(IConnectionsService connectionsService, string historyPath)
    {
        ArgumentNullException.ThrowIfNull(connectionsService);
        ArgumentException.ThrowIfNullOrWhiteSpace(historyPath);
        _connectionsService = connectionsService;
        _historyPath = historyPath;
    }

    public string LastError { get; private set; } = string.Empty;

    public void Configure(bool enabled, int retentionDays)
    {
        lock (_monitorLock)
        {
            _isEnabled = enabled;
            _retentionDays = Math.Clamp(retentionDays, 1, 365);
            UpdateMonitorLocked();
        }
    }

    public void SetWindowVisible(bool isVisible)
    {
        lock (_monitorLock)
        {
            _isWindowVisible = isVisible;
            UpdateMonitorLocked();
        }
    }

    public async Task<IReadOnlyList<ConnectionHistoryEntry>> GetEntriesAsync(string searchText = "")
    {
        await FileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await using var connection = await OpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT connection_key, application, protocol, local_address, local_port,
                       remote_address, remote_port, state, direction, first_seen, last_seen
                FROM history
                ORDER BY last_seen DESC;
                """;
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            var entries = new List<ConnectionHistoryEntry>();
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var entry = new ConnectionHistoryEntry(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2),
                    reader.GetString(3), reader.GetString(4), reader.GetString(5),
                    reader.GetString(6), reader.GetString(7), reader.GetString(8),
                    DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    DateTimeOffset.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
                if (string.IsNullOrWhiteSpace(searchText)
                    || entry.Application.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)
                    || entry.RemoteAddress.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)
                    || entry.LocalAddress.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)
                    || entry.Protocol.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)
                    || entry.State.Contains(searchText, StringComparison.CurrentCultureIgnoreCase))
                    entries.Add(entry);
            }

            return entries;
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task ClearAsync()
    {
        await FileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(_historyPath))
            {
                LastError = string.Empty;
                return;
            }
            await using var connection = await OpenConnectionAsync().ConfigureAwait(false);
            await using var secureDelete = connection.CreateCommand();
            secureDelete.CommandText = "PRAGMA secure_delete=ON;";
            await secureDelete.ExecuteNonQueryAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM history; VACUUM;";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            LastError = string.Empty;
        }
        finally
        {
            FileLock.Release();
        }
    }

    private void UpdateMonitorLocked()
    {
        if (_isEnabled && _isWindowVisible)
        {
            if (_monitorCancellation is null)
            {
                _monitorCancellation = new CancellationTokenSource();
                _ = MonitorAsync(_monitorCancellation);
            }
        }
        else
        {
            _monitorCancellation?.Cancel();
            _monitorCancellation = null;
        }
    }

    private async Task MonitorAsync(CancellationTokenSource source)
    {
        var cancellationToken = source.Token;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var rows = await _connectionsService.GetConnectionsAsync(
                        new ConnectionQuery(true, true, true, string.Empty), cancellationToken).ConfigureAwait(false);
                    await RecordSnapshotAsync(rows, cancellationToken).ConfigureAwait(false);
                    LastError = string.Empty;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LastError = $"Connection history could not be updated: {ex.Message}";
                    Debug.WriteLine(LastError);
                }

                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            source.Dispose();
        }
    }

    internal async Task RecordSnapshotAsync(IReadOnlyList<ConnectionRow> rows, CancellationToken cancellationToken = default)
    {
        await FileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = await OpenConnectionAsync().ConfigureAwait(false);
            using var transaction = connection.BeginTransaction();
            await using var upsert = connection.CreateCommand();
            upsert.Transaction = transaction;
            upsert.CommandText = """
                INSERT INTO history (
                    connection_key, application, protocol, local_address, local_port,
                    remote_address, remote_port, state, direction, first_seen, last_seen)
                VALUES ($key, $application, $protocol, $localAddress, $localPort,
                    $remoteAddress, $remotePort, $state, $direction, $seen, $seen)
                ON CONFLICT(connection_key) DO UPDATE SET
                    last_seen = excluded.last_seen;
                """;
            var parameters = new[]
            {
                ("$key", (object?)null), ("$application", null), ("$protocol", null),
                ("$localAddress", null), ("$localPort", null), ("$remoteAddress", null),
                ("$remotePort", null), ("$state", null), ("$direction", null), ("$seen", null)
            };
            foreach (var (name, _) in parameters)
                upsert.Parameters.Add(name, SqliteType.Text);

            var seenAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var values = new object?[]
                {
                    ConnectionHistoryEntry.CreateKey(row), row.Application, row.Protocol,
                    row.LocalAddress, row.LocalPort, row.RemoteAddress, row.RemotePort,
                    row.State, row.Direction, seenAt
                };
                for (var index = 0; index < values.Length; index++)
                    upsert.Parameters[index].Value = values[index] ?? DBNull.Value;
                await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using var prune = connection.CreateCommand();
            prune.Transaction = transaction;
            prune.CommandText = """
                DELETE FROM history WHERE last_seen < $cutoff;
                DELETE FROM history
                WHERE connection_key NOT IN (
                    SELECT connection_key FROM history ORDER BY last_seen DESC LIMIT $maximum);
                """;
            prune.Parameters.AddWithValue("$cutoff",
                DateTimeOffset.UtcNow.AddDays(-Volatile.Read(ref _retentionDays)).ToString("O", CultureInfo.InvariantCulture));
            prune.Parameters.AddWithValue("$maximum", MaximumEntries);
            await prune.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            FileLock.Release();
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        _ = SqliteProvider.Value;
        var directory = Path.GetDirectoryName(_historyPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _historyPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        try
        {
            await connection.OpenAsync().ConfigureAwait(false);
            await using (var privacy = connection.CreateCommand())
            {
                privacy.CommandText = "PRAGMA secure_delete=ON;";
                await privacy.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            if (!_schemaInitialized)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE IF NOT EXISTS history (
                        connection_key TEXT PRIMARY KEY COLLATE NOCASE,
                        application TEXT NOT NULL,
                        protocol TEXT NOT NULL,
                        local_address TEXT NOT NULL,
                        local_port TEXT NOT NULL,
                        remote_address TEXT NOT NULL,
                        remote_port TEXT NOT NULL,
                        state TEXT NOT NULL,
                        direction TEXT NOT NULL,
                        first_seen TEXT NOT NULL,
                        last_seen TEXT NOT NULL);
                    CREATE INDEX IF NOT EXISTS ix_history_last_seen ON history(last_seen DESC);
                    """;
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                _schemaInitialized = true;
            }
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}

internal sealed record ConnectionHistoryEntry(
    string Key,
    string Application,
    string Protocol,
    string LocalAddress,
    string LocalPort,
    string RemoteAddress,
    string RemotePort,
    string State,
    string Direction,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen)
{
    public string FirstSeenText => FirstSeen.ToLocalTime().ToString("g");
    public string LastSeenText => LastSeen.ToLocalTime().ToString("g");
    public string LocalEndpoint => $"{LocalAddress}:{LocalPort}";
    public string RemoteEndpoint => $"{RemoteAddress}:{RemotePort}";

    public static string CreateKey(ConnectionRow row) =>
        string.Join("|", row.Application, row.Protocol, row.LocalAddress, row.LocalPort,
            row.RemoteAddress, row.RemotePort, row.State, row.Direction);
}
