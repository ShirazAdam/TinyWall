using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.Exceptions;
using ModernTinyWall.TinyWall;
using System;
using System.Threading;
using System.Threading.Tasks;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class TinyWallExceptionStoreTests
{
    [TestMethod]
    public async Task GetExceptionsWhenSettingsCannotBeLoadedThenThrows()
    {
        var client = new FakeExceptionSettingsClient(MessageType.COM_ERROR);
        var store = new TinyWallExceptionStore(client);

        try
        {
            await store.GetExceptionsAsync(new ExceptionQuery(string.Empty));
            Assert.Fail("Expected settings-load failure to be reported.");
        }
        catch (InvalidOperationException exception)
        {
            StringAssert.Contains(exception.Message, "Could not load TinyWall settings");
        }
    }

    [TestMethod]
    public async Task ApplyEntryActionWhenDuplicateIsNotReplacedThenReportsNoChange()
    {
        const string executablePath = @"C:\Program Files\App\app.exe";
        var client = new FakeExceptionSettingsClient(MessageType.GET_SETTINGS);
        client.Config.ActiveProfile.AppExceptions.Add(
            new FirewallExceptionV3(new ExecutableSubject(executablePath), HardBlockPolicy.Instance));
        var store = new TinyWallExceptionStore(client);
        var request = new ExceptionEntryActionRequest(
            ExceptionEntryKind.Executable,
            ExceptionEntryPolicy.Allow,
            "app.exe",
            executablePath);

        var result = await store.ApplyEntryActionAsync(request, replaceExisting: false);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Message, "already exists");
        Assert.HasCount(1, client.Config.ActiveProfile.AppExceptions);
        Assert.IsFalse(client.SetConfigCalled);
    }

    [TestMethod]
    public async Task ApplyEntryActionWhenDuplicateIsReplacedThenUpdatesPolicy()
    {
        const string executablePath = @"C:\Program Files\App\app.exe";
        var client = new FakeExceptionSettingsClient(MessageType.GET_SETTINGS);
        client.Config.ActiveProfile.AppExceptions.Add(
            new FirewallExceptionV3(new ExecutableSubject(executablePath), HardBlockPolicy.Instance));
        var store = new TinyWallExceptionStore(client);
        var request = new ExceptionEntryActionRequest(
            ExceptionEntryKind.Executable,
            ExceptionEntryPolicy.Allow,
            "app.exe",
            executablePath);

        var result = await store.ApplyEntryActionAsync(request, replaceExisting: true);

        Assert.IsTrue(result.Success);
        Assert.HasCount(1, client.Config.ActiveProfile.AppExceptions);
        Assert.IsInstanceOfType<TcpUdpPolicy>(client.Config.ActiveProfile.AppExceptions[0].Policy);
        Assert.IsTrue(client.SetConfigCalled);
    }

    [TestMethod]
    public async Task ApplyEntryActionWhenServiceIsLockedThenReportsFailure()
    {
        var client = new FakeExceptionSettingsClient(MessageType.GET_SETTINGS)
        {
            SetResponse = MessageType.RESPONSE_LOCKED
        };
        var store = new TinyWallExceptionStore(client);
        var request = new ExceptionEntryActionRequest(
            ExceptionEntryKind.Executable,
            ExceptionEntryPolicy.Allow,
            "app.exe",
            @"C:\Program Files\App\app.exe");

        var result = await store.ApplyEntryActionAsync(request, replaceExisting: false);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Message, "locked");
        Assert.IsTrue(client.SetConfigCalled);
    }

    [TestMethod]
    public async Task UpdateExceptionWhenEntryExistsThenUpdatesIt()
    {
        const string executablePath = @"C:\Program Files\App\app.exe";
        var client = new FakeExceptionSettingsClient(MessageType.GET_SETTINGS);
        var existing = new FirewallExceptionV3(new ExecutableSubject(executablePath), HardBlockPolicy.Instance);
        client.Config.ActiveProfile.AppExceptions.Add(existing);
        var store = new TinyWallExceptionStore(client);

        var result = await store.UpdateExceptionAsync(
            existing.Id,
            new ExceptionEditRequest("Executable", "app.exe", executablePath, "TCP/UDP", RemoteTcpPorts: "443"));

        Assert.IsTrue(result.Success);
        Assert.AreEqual("TCP", ExceptionDescriptor.CreateRow(client.Config.ActiveProfile.AppExceptions[0]).Policy);
        Assert.IsTrue(client.SetConfigCalled);
    }

    [TestMethod]
    public async Task ApplyEntryActionWhenCancelledThenDoesNotContactService()
    {
        var client = new FakeExceptionSettingsClient(MessageType.GET_SETTINGS);
        var store = new TinyWallExceptionStore(client);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        try
        {
            await store.ApplyEntryActionAsync(
                new ExceptionEntryActionRequest(
                    ExceptionEntryKind.Executable,
                    ExceptionEntryPolicy.Allow,
                    "app.exe",
                    @"C:\Program Files\App\app.exe"),
                replaceExisting: false,
                cancellationToken: cancellationTokenSource.Token);
            Assert.Fail("Expected the operation to be cancelled.");
        }
        catch (OperationCanceledException)
        {
            Assert.IsFalse(client.GetConfigCalled);
        }
    }

    private sealed class FakeExceptionSettingsClient(MessageType getResponse) : IExceptionSettingsClient
    {
        public ServerConfiguration Config { get; } = new()
        {
            ActiveProfileName = "Default"
        };

        public bool SetConfigCalled { get; private set; }
        public bool GetConfigCalled { get; private set; }
        public MessageType SetResponse { get; set; } = MessageType.PUT_SETTINGS;

        public MessageType GetServerConfig(
            out ServerConfiguration? serverConfig,
            out ServerState? serverState,
            ref Guid clientChangeset)
        {
            GetConfigCalled = true;
            serverState = null;
            serverConfig = getResponse == MessageType.GET_SETTINGS ? Config : null;
            return getResponse;
        }

        public TwMessage SetServerConfig(ServerConfiguration serverConfig, Guid clientChangeset)
        {
            SetConfigCalled = true;
            return SetResponse switch
            {
                MessageType.RESPONSE_LOCKED => TwMessageLocked.Instance,
                MessageType.COM_ERROR => TwMessageComError.Instance,
                _ => TwMessagePutSettings.CreateRequest(clientChangeset, serverConfig)
            };
        }
    }
}
