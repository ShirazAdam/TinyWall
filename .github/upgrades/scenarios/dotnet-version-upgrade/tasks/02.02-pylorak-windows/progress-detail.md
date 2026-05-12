# 02.02-pylorak-windows Progress Detail

## Changes

- Retargeted `pylorak.Windows` from `net481` to `net10.0-windows`.
- Enabled Windows Forms support with `UseWindowsForms`.
- Removed `System.Memory` because the assessment reported that the package functionality is included by the .NET 10 framework reference.
- Removed the explicit `System.Windows.Forms` assembly reference because Windows Forms is now supplied through the Windows desktop framework support.
- Removed unsupported `RuntimeHelpers.PrepareConstrainedRegions()` calls from `Privilege.cs` while preserving the existing cleanup structure.

## Validation

- `dotnet build pylorak.Windows/pylorak.Windows.csproj`: passed.
- Build completed with warnings, including remaining `ReliabilityContractAttribute`/CER warnings in `Privilege.cs` and `SafeHandles.cs`, plus nullable warnings.

## Notes

The project now compiles on .NET 10 for Windows. Remaining CER-related attributes are warnings rather than build blockers and may be cleaned up in a later hardening pass if desired.
