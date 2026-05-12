# TinyWall Inno Setup Packaging

This folder contains the Inno Setup packaging entry point for the .NET 10 TinyWall application.

## Prerequisites

- Inno Setup 6 or later.
- .NET 10 Windows Desktop Runtime on target machines for framework-dependent installers.
- Published TinyWall output under `artifacts\publish\TinyWall\<runtime>`.

## Publish Inputs

Create framework-dependent publish outputs before building installers:

```powershell
dotnet publish ..\TinyWall\TinyWall.csproj -c Release -r win-x86 --self-contained false -o ..\artifacts\publish\TinyWall\win-x86
dotnet publish ..\TinyWall\TinyWall.csproj -c Release -r win-x64 --self-contained false -o ..\artifacts\publish\TinyWall\win-x64
dotnet publish ..\TinyWall\TinyWall.csproj -c Release -r win-arm64 --self-contained false -o ..\artifacts\publish\TinyWall\win-arm64
```

## Build Installers

From this directory, build architecture-specific installers with `ISCC.exe`:

```powershell
ISCC.exe .\TinyWall.iss /DSourceArch=win-x86 /DOutputArch=win-x86
ISCC.exe .\TinyWall.iss /DSourceArch=win-x64 /DOutputArch=win-x64
ISCC.exe .\TinyWall.iss /DSourceArch=win-arm64 /DOutputArch=win-arm64
```

Outputs are written to `artifacts\installer\<runtime>`.

## Notes

- The installer requires administrator privileges because TinyWall manages a Windows service and firewall state.
- The script targets Windows 10 and Windows 11.
- The old Visual Studio setup project is no longer the primary installer path.
- If self-contained deployment is preferred, publish with `--self-contained true` before building the installer.
