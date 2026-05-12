# 02.03-pylorak-windows-services: Upgrade pylorak.Windows.Services

## Objective
Upgrade `pylorak.Windows.Services` after `pylorak.Windows` is stable.

## Scope
- Retarget `pylorak.Windows.Services.csproj` to `net10.0`.
- Preserve the project reference to `pylorak.Windows`.
- Review service process and Code Access Security compatibility findings.
- Restore and build the project.

## Done When
`pylorak.Windows.Services` targets `net10.0`, compatibility issues are addressed or documented, and the project builds successfully.
