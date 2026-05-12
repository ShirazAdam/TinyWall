# 02.03-pylorak-windows-services Progress Detail

## Changes

- Retargeted `pylorak.Windows.Services` from `net481` to `net10.0-windows` because it references the Windows-specific `pylorak.Windows` project and the solution intentionally targets Windows 10 and Windows 11 only.
- Replaced the legacy `System.ServiceProcess` assembly reference with the `System.ServiceProcess.ServiceController` package.
- Removed Code Access Security `SecurityPermission` link demands from `ServiceControlManager.cs` because CAS is unsupported in modern .NET.
- Removed unsupported `ReliabilityContract`/CER annotations from service safe-handle code.
- Removed the legacy `ServiceProcessInstaller` installer annotation because that installer component is not available in modern .NET.

## Validation

- `dotnet build pylorak.Windows.Services/pylorak.Windows.Services.csproj`: passed.
- Build completed with nullable warnings in `ServiceControlManager.cs` and `ServiceBase.cs`.

## Notes

The Windows-specific TFM is intentional because the user clarified that the upgraded solution targets Windows 10 and Windows 11 only.
