namespace ModernTinyWall.TinyWall;

public sealed record ConnectionRow(
    string Application,
    string ExecutablePath,
    string Protocol,
    string LocalPort,
    string LocalAddress,
    string RemotePort,
    string RemoteAddress,
    string State,
    string Direction);
