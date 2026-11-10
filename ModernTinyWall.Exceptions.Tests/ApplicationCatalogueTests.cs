using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.TinyWall;
using ModernTinyWall.TinyWall.DatabaseClasses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class ApplicationCatalogueTests
{
    private string _directory = null!;
    private string DatabasePath => Path.Combine(_directory, "profiles.db");
    private string LegacyPath => Path.Combine(_directory, "profiles.json");
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "profiles.json");

    [TestInitialize]
    public void Initialise()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"TinyWall.Catalogue.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_directory, true);

    [TestMethod]
    public void RealCatalogueRoundTripsApplicationsFlagsComponentsAndPolymorphicPolicies()
    {
        var original = SqliteApplicationCatalogue.Import(FixturePath);
        Assert.IsNotEmpty(original.KnownApplications);
        original.Save(DatabasePath);
        var restored = SqliteApplicationCatalogue.Load(DatabasePath);
        Assert.AreEqual(original.SourceHash, restored.SourceHash);
        Assert.HasCount(original.KnownApplications.Count, restored.KnownApplications);
        for (var index = 0; index < original.KnownApplications.Count; index++)
        {
            var before = original.KnownApplications[index];
            var after = restored.KnownApplications[index];
            Assert.AreEqual(before.Name, after.Name);
            Assert.HasCount(before.Components.Count, after.Components);
            if (before.Flags is null)
                Assert.IsNull(after.Flags);
            else
                CollectionAssert.AreEquivalent(before.Flags.ToArray(), after.Flags!.ToArray());
            for (var position = 0; position < before.Components.Count; position++)
                CollectionAssert.AreEqual(SerialisationHelper.Serialise(before.Components[position]),
                    SerialisationHelper.Serialise(after.Components[position]));
        }
        using var connection = new SqliteConnection($"Data Source={DatabasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM applications;";
        Assert.AreEqual((long)original.KnownApplications.Count, (long)command.ExecuteScalar()!);
        command.CommandText = "SELECT COUNT(*) FROM components;";
        Assert.AreEqual((long)original.KnownApplications.Sum(app => app.Components.Count), (long)command.ExecuteScalar()!);
    }

    [TestMethod]
    public void MigrationPreservesLegacyBackupSettingsAndExistingDatabase()
    {
        File.Copy(FixturePath, LegacyPath);
        var settings = Path.Combine(_directory, "options.json");
        File.WriteAllText(settings, "{\"MinimiseToTray\":false}");
        var legacyHash = Hasher.HashFile(LegacyPath);
        var settingsHash = Hasher.HashFile(settings);
        SqliteApplicationCatalogue.EnsureMigrated(DatabasePath, LegacyPath, "unused-seed");
        var databaseHash = Hasher.HashFile(DatabasePath);
        Assert.AreEqual(legacyHash, Hasher.HashFile(LegacyPath));
        Assert.AreEqual(settingsHash, Hasher.HashFile(settings));
        File.WriteAllText(LegacyPath, "obsolete JSON must not overwrite an existing catalogue");
        SqliteApplicationCatalogue.EnsureMigrated(DatabasePath, LegacyPath, "unused-seed");
        Assert.AreEqual(databaseHash, Hasher.HashFile(DatabasePath));
        Assert.AreEqual(legacyHash, SqliteApplicationCatalogue.Load(DatabasePath).SourceHash);
    }

    [TestMethod]
    public void CleanInstallUsesReadOnlySqliteSeedWithoutChangingIt()
    {
        var seed = Path.Combine(_directory, "seed.db");
        SqliteApplicationCatalogue.Import(FixturePath).Save(seed);
        var seedHash = Hasher.HashFile(seed);
        SqliteApplicationCatalogue.EnsureMigrated(DatabasePath, LegacyPath, seed);
        Assert.AreEqual(seedHash, Hasher.HashFile(seed));
        Assert.AreEqual(seedHash, SqliteApplicationCatalogue.Load(DatabasePath).SourceHash);
        Assert.IsFalse(File.Exists(LegacyPath));
        Assert.IsEmpty(Directory.GetFiles(_directory, "*-wal"));
    }

    [TestMethod]
    public void ClientsCanReadWhileTheServiceHoldsItsReadOnlyFileProtection()
    {
        SqliteApplicationCatalogue.Import(FixturePath).Save(DatabasePath);
        var hash = Hasher.HashFile(DatabasePath);
        using var protection = new FileStream(DatabasePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.IsNotEmpty(SqliteApplicationCatalogue.Load(DatabasePath).KnownApplications);
        Assert.AreEqual(hash, Hasher.HashFile(DatabasePath));
        Assert.HasCount(1, Directory.GetFiles(_directory));
    }

    [TestMethod]
    public void MalformedMigrationAndReplacementLeavePreviousFilesIntact()
    {
        File.WriteAllText(LegacyPath, "{\"KnownApplications\":[null]}");
        var legacyHash = Hasher.HashFile(LegacyPath);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            SqliteApplicationCatalogue.EnsureMigrated(DatabasePath, LegacyPath, "unused-seed"));
        Assert.IsFalse(File.Exists(DatabasePath));
        Assert.AreEqual(legacyHash, Hasher.HashFile(LegacyPath));
        var valid = SqliteApplicationCatalogue.Import(FixturePath);
        valid.Save(DatabasePath);
        var databaseHash = Hasher.HashFile(DatabasePath);
        valid.KnownApplications[0].Name = " ";
        Assert.ThrowsExactly<InvalidDataException>(() => valid.Save(DatabasePath));
        Assert.AreEqual(databaseHash, Hasher.HashFile(DatabasePath));
        Assert.HasCount(2, Directory.GetFiles(_directory));
    }

    [TestMethod]
    public void NewerOrCorruptDatabasesAreRejectedRatherThanRecreatedFromJson()
    {
        SqliteApplicationCatalogue.Import(FixturePath).Save(DatabasePath);
        using (var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version=999;";
            command.ExecuteNonQuery();
        }
        var hash = Hasher.HashFile(DatabasePath);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            SqliteApplicationCatalogue.EnsureMigrated(DatabasePath, FixturePath, "unused-seed"));
        Assert.AreEqual(hash, Hasher.HashFile(DatabasePath));
        File.WriteAllText(DatabasePath, "not SQLite");
        Assert.ThrowsExactly<SqliteException>(() => SqliteApplicationCatalogue.Load(DatabasePath));
    }

    [TestMethod]
    public void StoresUnicodeQuotedNamesAndNullFlagsWithoutChangingIdentityRestrictions()
    {
        var component = new SubjectIdentity(new ExecutableSubject("example.exe"))
        {
            Policy = new HardBlockPolicy(),
            SearchPaths = [@"{folder:pf64}\Example"],
            AllowedSha1 = ["0123456789"],
            CertificateSubjects = null
        };
        var original = new AppDatabase([
            new() { Name = "\u00c5pp's catalogue", Flags = null, Components = [component] },
            new() { Name = "Special", Flags = new Dictionary<string, string?> { ["TWUI:SPECIAL"] = null, ["Label"] = "'quoted'" } }
        ]);
        original.Save(DatabasePath);
        var restored = SqliteApplicationCatalogue.Load(DatabasePath);
        Assert.AreEqual(original.KnownApplications[0].Name, restored.KnownApplications[0].Name);
        Assert.IsNull(restored.KnownApplications[0].Flags);
        Assert.IsTrue(restored.KnownApplications[1].HasFlag("TWUI:Special"));
        Assert.IsNull(restored.KnownApplications[1].Flags!["TWUI:SPECIAL"]);
        CollectionAssert.AreEqual(SerialisationHelper.Serialise(component),
            SerialisationHelper.Serialise(restored.KnownApplications[0].Components[0]));
    }

    [TestMethod]
    public void OfflineCompilerProducesSqliteAndRejectsBadInputWithoutReplacingIt()
    {
        Assert.AreEqual(0, CatalogueCommand.TryExecute(["/buildcatalogue", FixturePath, DatabasePath]));
        var hash = Hasher.HashFile(DatabasePath);
        Assert.IsNotEmpty(SqliteApplicationCatalogue.Load(DatabasePath).KnownApplications);
        File.WriteAllText(LegacyPath, "invalid JSON");
        Assert.AreEqual(1, CatalogueCommand.TryExecute(["/buildcatalogue", LegacyPath, DatabasePath]));
        Assert.AreEqual(hash, Hasher.HashFile(DatabasePath));
    }
}
