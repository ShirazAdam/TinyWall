# 01-sdk-environment: SDK Environment

## Objective

Validate that the .NET 10 SDK and repository configuration are ready before changing project files.

## Findings

- .NET 10 SDK validation succeeded: a compatible SDK is installed.
- No `global.json` file was found, so no SDK pin needs to be changed.
- No `Directory.Build.props` or `Directory.Build.targets` files were found by workspace search.
- Target framework properties and package references are project-specific.
- `TinyWallSetup.vdproj` is a Visual Studio setup project and should be reviewed during packaging, not retargeted as an SDK-style .NET project.

## Target Framework Mapping

- `pylorak.Utilities`: `net481` → `net10.0`
- `pylorak.Windows`: `net481` → `net10.0-windows`
- `pylorak.Windows.Services`: `net481` → `net10.0`
- `pylorak.Windows.WFP`: `net481` → `net10.0`
- `TinyWall`: `net481` → `net10.0-windows`

## Architecture Approach

Preserve x86, x64, and ARM support by keeping architecture-specific configuration visible during project and packaging changes. Runtime identifier, platform target, installer output, and any native or Windows-specific dependencies must be checked before validation completes.

## Next Tasks

Project file changes should begin with the foundation libraries, following dependency order.
