# 02.02-pylorak-windows: Upgrade pylorak.Windows

## Objective
Upgrade `pylorak.Windows` after `pylorak.Utilities` is stable.

## Scope
- Retarget `pylorak.Windows.csproj` to `net10.0-windows`.
- Enable Windows desktop / Windows Forms support as required.
- Preserve the project reference to `pylorak.Utilities`.
- Review package references, Windows Forms, GDI+ / System.Drawing, and Code Access Security findings.
- Restore and build the project.

## Done When
`pylorak.Windows` targets `net10.0-windows`, Windows desktop support is configured, compatibility issues are addressed or documented, and the project builds successfully.
