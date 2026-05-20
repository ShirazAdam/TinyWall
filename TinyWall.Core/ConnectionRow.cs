namespace pylorak.TinyWall;

public sealed record ConnectionRow(
    string Application,
    string Protocol,
    string LocalPort,
    string LocalAddress,
    string RemotePort,
    string RemoteAddress,
    string State,
    string Direction);
