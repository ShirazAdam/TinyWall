# 04-tinywall-application Progress Detail

## Changes

- Retargeted `TinyWall` from `net481` to `net10.0-windows10.0.26100.0` for the Windows 10/11-only product target.
- Updated .NET-aligned packages:
  - `Microsoft.Extensions.DependencyInjection` to `10.0.8`
  - `Microsoft.Extensions.DependencyInjection.Abstractions` to `10.0.8`
  - `Microsoft.Extensions.Options` to `10.0.8`
- Removed framework-included packages and legacy assembly references.
- Added modern support packages for WMI, service control, Windows Runtime access, and named-pipe access control.
- Replaced unsupported `COMReference` items with generated interop assembly references checked into `TinyWall/Interop`.
- Excluded legacy `System.Configuration.Install` installer classes from compilation.
- Replaced `ManagedInstallerClass` install/uninstall calls with `sc.exe` service creation/deletion.
- Replaced CNG-specific hash classes with `SHA256.Create()` and `SHA1.Create()`.
- Updated named-pipe security creation to use `NamedPipeServerStreamAcl.Create`.
- Updated list parsing in `TinyWallService` to avoid .NET 10 span split range semantics where the previous code expected spans.
- Disambiguated custom `Microsoft.Samples.TaskDialog` and `TaskDialogIcon` usages from modern Windows Forms types.

## Validation

- `dotnet build TinyWall/TinyWall.csproj`: passed.
- Build completed with warnings, mainly nullable warnings, remaining linked-source CER warnings, and a WinForms high-DPI manifest warning.

## Notes

The application now builds on .NET 10. The legacy installer classes are excluded from compilation and should be reviewed in the packaging task. Generated COM interop assemblies are now project inputs because SDK-style `dotnet build` does not support resolving `COMReference` items directly.
