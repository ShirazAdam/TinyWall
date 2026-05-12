# 03-windows-filtering-library Progress Detail

## Changes

- Retargeted `pylorak.Windows.WFP` from `net481` to `net10.0-windows`, consistent with the Windows 10 and Windows 11-only product target.
- Removed unsupported Constrained Execution Region-related imports and attributes from WFP interop and safe-handle code.
- Replaced obsolete `SHA1Managed` usage with `SHA1.HashData`.

## Validation

- `dotnet build pylorak.Windows.WFP/pylorak.Windows.WFP.csproj`: passed.
- Build completed with nullable warnings in `SafeHandles.cs` and `PInvokeHelper.cs`.

## Notes

The WFP library is Windows-specific by design, so the Windows-specific .NET 10 target framework is intentional.
