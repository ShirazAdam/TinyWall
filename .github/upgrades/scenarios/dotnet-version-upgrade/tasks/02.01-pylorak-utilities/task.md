# 02.01-pylorak-utilities: Upgrade pylorak.Utilities

## Objective
Upgrade `pylorak.Utilities` from `net481` to `net10.0` as the leaf foundation library.

## Scope
- Retarget `pylorak.Utilities.csproj` to `net10.0`.
- Remove package references whose functionality is included by the target framework where the assessment recommends it.
- Review source compatibility issues, especially legacy cryptography usage.
- Restore and build the project.

## Done When
`pylorak.Utilities` targets `net10.0`, relevant package changes are applied, source compatibility issues are addressed or documented, and the project builds successfully.
