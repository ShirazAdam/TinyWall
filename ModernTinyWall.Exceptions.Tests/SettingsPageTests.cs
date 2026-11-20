using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.Models;
using ModernTinyWall.Services;
using ModernTinyWall.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class SettingsPageTests
{
    [TestMethod]
    public async Task InitialLoadKeepsReadOnlyContentAndSharesThePendingRequest()
    {
        var completion = new TaskCompletionSource<IReadOnlyList<SettingsSection>>();
        var service = new FakeSettingsService { PendingRead = completion.Task };
        var initialSections = CreateSections(false);
        var viewModel = new SettingsPageViewModel(service, new FakeMaintenanceService(), initialSections);

        var first = viewModel.EnsureLoadedAsync();
        var second = viewModel.EnsureLoadedAsync();

        Assert.AreSame(first, second);
        Assert.AreEqual(1, service.ReadCount);
        Assert.AreSame(initialSections[0], viewModel.Sections[0]);
        Assert.IsFalse(viewModel.Sections[0].Items[0].IsEditable);

        completion.SetResult(CreateSections());
        await first;

        Assert.IsTrue(viewModel.Sections[0].Items[0].IsEditable);
        Assert.IsFalse(viewModel.IsLoading);
    }

    [TestMethod]
    public async Task RepeatedVisitsCompleteSynchronouslyWithoutReadsOrCollectionChanges()
    {
        var service = new FakeSettingsService();
        var viewModel = new SettingsPageViewModel(service, new FakeMaintenanceService(), SettingsService.CreateFallbackSections());
        await viewModel.EnsureLoadedAsync();
        var section = viewModel.Sections[0];
        var collectionChanges = 0;
        var propertyChanges = 0;
        viewModel.SpecialProfiles.CollectionChanged += (_, _) => collectionChanges++;
        viewModel.PropertyChanged += (_, _) => propertyChanges++;

        for (var visit = 0; visit < 1_000; visit++)
            Assert.IsTrue(viewModel.EnsureLoadedAsync().IsCompletedSuccessfully);

        Assert.AreEqual(1, service.ReadCount);
        Assert.AreEqual(0, collectionChanges);
        Assert.AreEqual(0, propertyChanges);
        Assert.AreSame(section, viewModel.Sections[0]);
    }

    [TestMethod]
    public async Task CancelDiscardsEditsAndApplyAndImportReloadSettings()
    {
        var service = new FakeSettingsService();
        var viewModel = new SettingsPageViewModel(service, new FakeMaintenanceService(), SettingsService.CreateFallbackSections());
        await viewModel.EnsureLoadedAsync();
        var original = viewModel.Sections[0];
        original.Items[0].IsEnabled = false;

        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.AreEqual(2, service.ReadCount);
        Assert.AreSame(original, viewModel.Sections[0]);
        Assert.IsTrue(viewModel.Sections[0].Items[0].IsEnabled);

        await viewModel.ApplyAsync();
        Assert.AreEqual(1, service.ApplyCount);
        Assert.AreEqual(3, service.ReadCount);

        await viewModel.ImportSettingsAsync("settings.tws");
        Assert.AreEqual(4, service.ReadCount);
    }

    [TestMethod]
    public async Task LoadFailureIsVisibleWithoutRepeatedDelaysAndCanBeRetried()
    {
        var service = new FakeSettingsService { ReadFailure = new InvalidOperationException("Service unavailable.") };
        var viewModel = new SettingsPageViewModel(service, new FakeMaintenanceService(), SettingsService.CreateFallbackSections());
        await viewModel.EnsureLoadedAsync();

        StringAssert.Contains(viewModel.StatusMessage, "Service unavailable.");
        Assert.IsFalse(viewModel.ApplyCommand.CanExecute(null));
        Assert.IsTrue(viewModel.EnsureLoadedAsync().IsCompletedSuccessfully);
        Assert.AreEqual(1, service.ReadCount);

        service.ReadFailure = null;
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.AreEqual(2, service.ReadCount);
        Assert.IsTrue(viewModel.AutomaticUpdateChecks.IsEditable);
        Assert.AreEqual("Settings ready.", viewModel.StatusMessage);
    }

    [TestMethod]
    public async Task DynamicSpecialProfilesRemainEditableAndAreIncludedWhenApplying()
    {
        var profile = new SettingsItem(SettingsService.SpecialProfilePrefix + "Test_Profile",
            "Test Profile", "Special profile", true, true);
        IReadOnlyList<SettingsSection> loaded = [.. CreateSections(), new("Special profiles", "", [profile])];
        var service = new FakeSettingsService { PendingRead = Task.FromResult(loaded) };
        var viewModel = new SettingsPageViewModel(service, new FakeMaintenanceService(), SettingsService.CreateFallbackSections());

        await viewModel.EnsureLoadedAsync();
        Assert.AreSame(profile, viewModel.SpecialProfiles[0]);
        profile.IsEnabled = false;
        await viewModel.ApplyAsync();

        Assert.IsNotNull(service.AppliedSections);
        Assert.IsFalse(service.AppliedSections.SelectMany(section => section.Items).Single(item => item.Key == profile.Key).IsEnabled);
    }

    private static IReadOnlyList<SettingsSection> CreateSections(bool editable = true)
    {
        var sections = SettingsService.CreateFallbackSections();
        sections[0].Items[0].IsEnabled = true;
        sections[0].Items[0].IsEditable = editable;
        return sections;
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public int ReadCount { get; private set; }
        public int ApplyCount { get; private set; }
        public Exception? ReadFailure { get; set; }
        public Task<IReadOnlyList<SettingsSection>>? PendingRead { get; init; }
        public IReadOnlyList<SettingsSection>? AppliedSections { get; private set; }

        public Task<IReadOnlyList<SettingsSection>> GetSettingsSectionsAsync(CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return ReadFailure is { } error
                ? Task.FromException<IReadOnlyList<SettingsSection>>(error)
                : PendingRead ?? Task.FromResult(CreateSections());
        }

        public Task<SettingsApplyResult> ApplySettingsAsync(IReadOnlyList<SettingsSection> sections,
            CancellationToken cancellationToken = default)
        {
            ApplyCount++;
            AppliedSections = sections;
            return Task.FromResult(new SettingsApplyResult(true, "Settings applied."));
        }
    }

    private sealed class FakeMaintenanceService : IMaintenanceService
    {
        public Task<MaintenanceResult> ImportSettingsAsync(string filePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MaintenanceResult(true, "Settings imported."));

        public Task<MaintenanceResult> ExportSettingsAsync(string filePath, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MaintenanceResult> ChangePasswordAsync(string newPassword, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MaintenanceResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
