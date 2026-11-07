using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ModernTinyWall.TinyWall;

internal readonly record struct DataUsageDelta(
    DateOnly Day,
    string ApplicationPath,
    long InternetReceived,
    long InternetSent,
    long LocalReceived,
    long LocalSent)
{
    public DataUsageDelta Add(long bytes, bool outgoing, bool local)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        return this with
        {
            InternetReceived = checked(InternetReceived + (!local && !outgoing ? bytes : 0)),
            InternetSent = checked(InternetSent + (!local && outgoing ? bytes : 0)),
            LocalReceived = checked(LocalReceived + (local && !outgoing ? bytes : 0)),
            LocalSent = checked(LocalSent + (local && outgoing ? bytes : 0))
        };
    }
}

internal sealed class DataUsageHistory
{
    private readonly string _connectionString;

    public DataUsageHistory(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = FULL;
            CREATE TABLE IF NOT EXISTS usage (
                day TEXT NOT NULL,
                application_key TEXT NOT NULL,
                application TEXT NOT NULL,
                internet_received INTEGER NOT NULL CHECK(internet_received >= 0),
                internet_sent INTEGER NOT NULL CHECK(internet_sent >= 0),
                local_received INTEGER NOT NULL CHECK(local_received >= 0),
                local_sent INTEGER NOT NULL CHECK(local_sent >= 0),
                PRIMARY KEY(day, application_key)
            ) STRICT;
            CREATE TABLE IF NOT EXISTS metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL) STRICT;
            """;
        command.ExecuteNonQuery();
    }

    public void MarkTrackingStarted(DateTimeOffset started)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO metadata(key, value)
                SELECT 'warning', 'Collection previously ended without a clean shutdown. Recorded history may be incomplete.'
                WHERE EXISTS (SELECT 1 FROM metadata WHERE key = 'active' AND value = '1');
            INSERT OR IGNORE INTO metadata(key, value) VALUES ('started', $started);
            INSERT OR REPLACE INTO metadata(key, value) VALUES ('active', '1');
            """;
        command.Parameters.AddWithValue("$started", started.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public void MarkTrackingStopped()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO metadata(key, value) VALUES ('active', '0');";
        command.ExecuteNonQuery();
    }

    public void MarkIncomplete(string warning)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO metadata(key, value) VALUES ('warning', $warning);";
        command.Parameters.AddWithValue("$warning", warning);
        command.ExecuteNonQuery();
    }

    public void Append(IReadOnlyCollection<DataUsageDelta> deltas)
    {
        if (deltas.Count == 0)
            return;

        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO usage VALUES ($day, $key, $application, $ir, $is, $lr, $ls)
            ON CONFLICT(day, application_key) DO UPDATE SET
                internet_received = internet_received + excluded.internet_received,
                internet_sent = internet_sent + excluded.internet_sent,
                local_received = local_received + excluded.local_received,
                local_sent = local_sent + excluded.local_sent;
            """;
        var day = command.Parameters.Add("$day", SqliteType.Text);
        var key = command.Parameters.Add("$key", SqliteType.Text);
        var application = command.Parameters.Add("$application", SqliteType.Text);
        var ir = command.Parameters.Add("$ir", SqliteType.Integer);
        var sent = command.Parameters.Add("$is", SqliteType.Integer);
        var lr = command.Parameters.Add("$lr", SqliteType.Integer);
        var ls = command.Parameters.Add("$ls", SqliteType.Integer);
        foreach (var delta in deltas)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(delta.ApplicationPath);
            if (delta.InternetReceived < 0 || delta.InternetSent < 0 || delta.LocalReceived < 0 || delta.LocalSent < 0)
                throw new ArgumentOutOfRangeException(nameof(deltas), "Usage counters cannot be negative.");
            day.Value = delta.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            key.Value = delta.ApplicationPath.ToUpperInvariant();
            application.Value = delta.ApplicationPath;
            ir.Value = delta.InternetReceived;
            sent.Value = delta.InternetSent;
            lr.Value = delta.LocalReceived;
            ls.Value = delta.LocalSent;
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public DataUsageSnapshot Query(DataUsagePeriod period, DateOnly today, string? error = null)
    {
        var firstDay = period switch
        {
            DataUsagePeriod.Today => today,
            DataUsagePeriod.Last7Days => today.AddDays(-6),
            DataUsagePeriod.Last30Days => today.AddDays(-29),
            DataUsagePeriod.AllTime => DateOnly.MinValue,
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT MIN(application), SUM(internet_received), SUM(internet_sent), SUM(local_received), SUM(local_sent)
            FROM usage WHERE day >= $first AND day <= $today
            GROUP BY application_key
            HAVING SUM(internet_received + internet_sent + local_received + local_sent) > 0
            ORDER BY SUM(internet_received + internet_sent + local_received + local_sent) DESC, MIN(application);
            """;
        command.Parameters.AddWithValue("$first", firstDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$today", today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var rows = new List<ApplicationDataUsage>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                rows.Add(new(reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4)));
        }

        command.Parameters.Clear();
        command.CommandText = "SELECT value FROM metadata WHERE key = 'started';";
        var startedText = command.ExecuteScalar() as string;
        DateTimeOffset? started = startedText is null ? null : DateTimeOffset.Parse(startedText, CultureInfo.InvariantCulture);
        command.CommandText = "SELECT value FROM metadata WHERE key = 'warning';";
        var warning = command.ExecuteScalar() as string;
        transaction.Commit();
        return new([.. rows], started, error, warning);
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
}
