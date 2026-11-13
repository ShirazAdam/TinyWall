using Microsoft.Data.Sqlite;
using ModernTinyWall.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ModernTinyWall.TinyWall.DatabaseClasses;

internal static class SqliteApplicationCatalogue
{
    private const int ApplicationId = 0x54574341;
    private const int SchemaVersion = 1;

    internal static AppDatabase Import(string sourcePath)
    {
        using var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = Hasher.HashStream(stream);
        stream.Position = 0;
        Span<byte> header = stackalloc byte[16];
        var isSqlite = stream.Read(header) == header.Length && header.SequenceEqual("SQLite format 3\0"u8);
        stream.Position = 0;
        var database = isSqlite ? Load(sourcePath) : SerialisationHelper.Deserialise(stream, new AppDatabase());
        Validate(database);
        database.SourceHash = hash;
        return database;
    }

    internal static void EnsureMigrated(string targetPath, string legacyPath, string seedPath)
    {
        if (File.Exists(targetPath))
        {
            Load(targetPath);
            return;
        }
        var source = File.Exists(legacyPath) ? legacyPath : seedPath;
        var database = Import(source);
        database.Save(targetPath);
    }

    internal static void Save(AppDatabase database, string path)
    {
        Validate(database);
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var updater = new AtomicFileUpdater(path);
        using (var connection = Open(updater.TemporaryFilePath, readOnly: false))
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"""
                PRAGMA journal_mode = DELETE;
                PRAGMA synchronous = FULL;
                PRAGMA application_id = {ApplicationId};
                PRAGMA user_version = {SchemaVersion};
                """;
            command.ExecuteNonQuery();
            using var transaction = connection.BeginTransaction();
            command.Transaction = transaction;
            command.CommandText = """
                CREATE TABLE applications (
                    id INTEGER PRIMARY KEY,
                    name TEXT NOT NULL CHECK(length(trim(name)) > 0),
                    flags_present INTEGER NOT NULL CHECK(flags_present IN (0, 1))
                ) STRICT;
                CREATE INDEX applications_name ON applications(name);
                CREATE TABLE application_flags (
                    application_id INTEGER NOT NULL REFERENCES applications(id),
                    name TEXT NOT NULL,
                    value TEXT,
                    PRIMARY KEY(application_id, name)
                ) STRICT;
                CREATE TABLE components (
                    application_id INTEGER NOT NULL REFERENCES applications(id),
                    position INTEGER NOT NULL CHECK(position >= 0),
                    definition TEXT NOT NULL CHECK(json_valid(definition)),
                    PRIMARY KEY(application_id, position)
                ) STRICT;
                CREATE TABLE metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL) STRICT;
                """;
            command.ExecuteNonQuery();
            for (var index = 0; index < database.KnownApplications.Count; index++)
            {
                var application = database.KnownApplications[index];
                command.CommandText = "INSERT INTO applications VALUES ($id, $name, $flags);";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$id", index);
                command.Parameters.AddWithValue("$name", application.Name);
                command.Parameters.AddWithValue("$flags", application.Flags is null ? 0 : 1);
                command.ExecuteNonQuery();
                if (application.Flags is not null)
                {
                    foreach (var flag in application.Flags)
                    {
                        command.CommandText = "INSERT INTO application_flags VALUES ($id, $name, $value);";
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("$id", index);
                        command.Parameters.AddWithValue("$name", flag.Key);
                        command.Parameters.AddWithValue("$value", (object?)flag.Value ?? DBNull.Value);
                        command.ExecuteNonQuery();
                    }
                }
                for (var position = 0; position < application.Components.Count; position++)
                {
                    command.CommandText = "INSERT INTO components VALUES ($id, $position, $definition);";
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("$id", index);
                    command.Parameters.AddWithValue("$position", position);
                    // Keep the existing polymorphic subject/policy encoding, not an opaque catalogue blob.
                    command.Parameters.AddWithValue("$definition", Encoding.UTF8.GetString(
                        SerialisationHelper.Serialise(application.Components[position])));
                    command.ExecuteNonQuery();
                }
            }
            if (database.SourceHash is not null)
            {
                command.CommandText = "INSERT INTO metadata VALUES ('source_hash', $hash);";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$hash", database.SourceHash);
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }
        Load(updater.TemporaryFilePath);
        updater.Commit();
    }

    internal static AppDatabase Load(string path)
    {
        using var connection = Open(path, readOnly: true);
        using var transaction = connection.BeginTransaction(deferred: true);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA application_id;";
        if (Convert.ToInt32(command.ExecuteScalar()) != ApplicationId)
            throw new InvalidDataException("The file is not a TinyWall application catalogue.");
        command.CommandText = "PRAGMA user_version;";
        if (Convert.ToInt32(command.ExecuteScalar()) != SchemaVersion)
            throw new InvalidDataException("The application catalogue schema version is unsupported.");
        command.CommandText = "PRAGMA quick_check;";
        if (!string.Equals(command.ExecuteScalar() as string, "ok", StringComparison.Ordinal))
            throw new InvalidDataException("The application catalogue failed its integrity check.");
        command.CommandText = "PRAGMA foreign_key_check;";
        using (var reader = command.ExecuteReader())
            if (reader.Read())
                throw new InvalidDataException("The application catalogue has invalid component references.");

        var applications = new List<Application>();
        var byId = new Dictionary<long, Application>();
        command.CommandText = "SELECT id, name, flags_present FROM applications ORDER BY id;";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var application = new Application
                {
                    Name = reader.GetString(1),
                    Flags = reader.GetInt64(2) == 1 ? new Dictionary<string, string?>() : null
                };
                applications.Add(application);
                byId.Add(reader.GetInt64(0), application);
            }
        }
        command.CommandText = "SELECT application_id, name, value FROM application_flags ORDER BY application_id, name;";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var flags = byId[reader.GetInt64(0)].Flags ??
                    throw new InvalidDataException("The application catalogue has inconsistent flags.");
                flags.Add(reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2));
            }
        }
        command.CommandText = "SELECT application_id, definition FROM components ORDER BY application_id, position;";
        using (var reader = command.ExecuteReader())
            while (reader.Read())
                byId[reader.GetInt64(0)].Components.Add(JsonSerializer.Deserialize(reader.GetString(1),
                    SourceGenerationContext.Default.SubjectIdentity) ??
                    throw new InvalidDataException("The application catalogue contains an empty component."));
        command.CommandText = "SELECT value FROM metadata WHERE key = 'source_hash';";
        var sourceHash = command.ExecuteScalar() as string;
        transaction.Commit();
        var database = new AppDatabase(applications) { SourceHash = sourceHash };
        Validate(database);
        return database;
    }

    private static SqliteConnection Open(string path, bool readOnly)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(path),
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            ForeignKeys = true
        }.ToString());
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

    private static void Validate(AppDatabase database)
    {
        if (database.KnownApplications is null)
            throw new InvalidDataException("The application catalogue has no application list.");
        foreach (var application in database.KnownApplications)
        {
            if (application is null || string.IsNullOrWhiteSpace(application.Name) || application.Components is null)
                throw new InvalidDataException("The application catalogue contains an invalid application.");
            foreach (var component in application.Components)
                if (component is null || component.Subject is null || component.Policy is null)
                    throw new InvalidDataException("The application catalogue contains an invalid component.");
        }
    }
}
