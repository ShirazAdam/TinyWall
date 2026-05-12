# 05-installer-packaging Progress Detail

## Review Summary

Reviewed `TinyWallSetup.vdproj` after the .NET 10 application upgrade and validated the current .NET 10 publish output shape with a `win-x64` release publish.

## Findings

- `TinyWallSetup.vdproj` is a Visual Studio setup project, not an SDK-style .NET project.
- The setup project still contains a Debug bootstrapper prerequisite for `.NET Framework 4.8.1`:
  - `Microsoft .NET Framework 4.8.1 (x86 and x64)`
- The Release configuration does not list a .NET Framework bootstrapper item in the reviewed section.
- The setup project packages the `TinyWall` project output group named `PublishItems`.
- `TinyWall` now produces .NET 10 publish output and includes generated COM interop assemblies as project references.
- Legacy installer classes that depended on `System.Configuration.Install` are excluded from compilation; service creation and deletion now use `sc.exe` in the application code.

## Packaging Implications

- For framework-dependent deployment, target machines need the appropriate .NET 10 Windows Desktop Runtime installed.
- For self-contained deployment, separate publishes should be produced per runtime identifier, such as:
  - `win-x86`
  - `win-x64`
  - `win-arm64`
- The current Visual Studio setup project has a single `TargetPlatform` value and does not directly express separate x86, x64, and ARM64 installer outputs.
- The existing Debug prerequisite should be updated or removed before shipping a Debug MSI because it still references .NET Framework 4.8.1.
- The setup project should be manually verified in Visual Studio because `.vdproj` projects are not buildable through `dotnet build`.

## Validation

- `dotnet publish TinyWall/TinyWall.csproj -c Release -r win-x64 --self-contained false`: passed.
- The publish validation confirms that the upgraded app can produce release output for installer input.

## Recommended Follow-up

- Decide whether installers should be framework-dependent or self-contained.
- Create architecture-specific packaging outputs for x86, x64, and ARM64 if separate installers are required.
- Replace the .NET Framework 4.8.1 bootstrapper with .NET 10 Windows Desktop Runtime installation guidance or a self-contained packaging strategy.
