using ModernTinyWall.TinyWall;
using System;

namespace ModernTinyWall.Exceptions;

internal interface IExceptionSettingsClient
{
    MessageType GetServerConfig(out ServerConfiguration? serverConfig, out ServerState? serverState, ref Guid clientChangeset);
    TwMessage SetServerConfig(ServerConfiguration serverConfig, Guid clientChangeset);
}

internal sealed class ExceptionSettingsClient : IExceptionSettingsClient
{
    private readonly Controller _controller;

    internal ExceptionSettingsClient(Controller controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        _controller = controller;
    }

    public MessageType GetServerConfig(out ServerConfiguration? serverConfig, out ServerState? serverState, ref Guid clientChangeset)
    {
        return _controller.GetServerConfig(out serverConfig, out serverState, ref clientChangeset);
    }

    public TwMessage SetServerConfig(ServerConfiguration serverConfig, Guid clientChangeset)
    {
        return _controller.SetServerConfig(serverConfig, clientChangeset);
    }
}
