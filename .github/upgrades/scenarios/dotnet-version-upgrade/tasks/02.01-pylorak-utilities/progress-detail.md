# 02.01-pylorak-utilities Progress Detail

## Changes

- Retargeted `pylorak.Utilities` from `net481` to `net10.0`.
- Removed `System.Memory` because the assessment reported that the package functionality is included by the .NET 10 framework reference.
- Updated `Pbkdf2` to use `Rfc2898DeriveBytes.Pbkdf2` with `HashAlgorithmName.SHA256`, avoiding obsolete PBKDF2 constructors.

## Validation

- `dotnet build pylorak.Utilities/pylorak.Utilities.csproj`: passed.
- Build completed with existing nullable warnings in `IpAddrMask.cs`, `EventMerger.cs`, `AtomicFileUpdater.cs`, and `HierarchicalStopwatch.cs`.

## Notes

The PBKDF2 storage marker now records `Rfc2898-SHA256`. Existing stored values may need compatibility handling if old `Rfc2898` hashes must still be verified later in the application upgrade.
