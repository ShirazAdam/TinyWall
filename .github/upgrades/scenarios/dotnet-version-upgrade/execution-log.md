
## [2026-05-12 20:40] 01-sdk-environment

Validated the upgrade environment for .NET 10. A compatible SDK is installed, no global.json constrains SDK selection, no shared Directory.Build.props or Directory.Build.targets files were found, and the target framework mapping for each project was documented. No implementation project files were changed.


## [2026-05-12 20:43] 02.01-pylorak-utilities

Upgraded pylorak.Utilities to net10.0, removed the framework-included System.Memory package, and modernised PBKDF2 hashing to use the static SHA-256 API. The project builds successfully with nullable warnings unrelated to the retargeting work.


## [2026-05-12 20:44] 02.02-pylorak-windows

Upgraded pylorak.Windows to net10.0-windows, enabled Windows Forms support, removed obsolete framework/package references, and removed unsupported CER preparation calls from Privilege.cs. The project builds successfully with remaining warnings documented.


## [2026-05-12 20:49] 02.03-pylorak-windows-services

Upgraded pylorak.Windows.Services to net10.0-windows, aligning it with the Windows-specific pylorak.Windows dependency and the Windows 10/11-only product target. Replaced the legacy System.ServiceProcess assembly reference with the supported ServiceController package, removed unsupported CAS/CER annotations, removed the unavailable ServiceProcessInstaller annotation, and validated that the project builds successfully with nullable warnings.


## [2026-05-12 20:50] 02.04-foundation-validation

Validated the upgraded foundation libraries by building pylorak.Windows.Services, which also built pylorak.Windows and pylorak.Utilities transitively. No applicable foundation test projects were discovered. The foundation group is ready for the next upgrade group.


## [2026-05-12 20:52] 03-windows-filtering-library

Upgraded pylorak.Windows.WFP to net10.0-windows, removed unsupported CER-related imports and attributes from WFP interop code, replaced obsolete SHA1Managed usage with SHA1.HashData, and validated that the project builds successfully with nullable warnings.


## [2026-05-12 21:04] 04-tinywall-application

Upgraded the TinyWall WinForms application to net10.0-windows10.0.26100.0, updated .NET-aligned packages, replaced unsupported COMReference items with generated interop assemblies, excluded legacy installer classes, replaced ManagedInstallerClass calls with sc.exe service management, modernised hashing and named-pipe access-control usage, fixed span split and TaskDialog compatibility issues, and validated that the application builds successfully with warnings.


## [2026-05-12 21:07] 05-installer-packaging

Reviewed TinyWallSetup.vdproj after the .NET 10 upgrade. The setup project remains a Visual Studio setup project and still references a .NET Framework 4.8.1 prerequisite in Debug. A win-x64 Release framework-dependent publish of TinyWall succeeded, confirming the application output can be produced for packaging. Follow-up packaging decisions are documented for framework-dependent versus self-contained deployment and x86, x64, and ARM64 installer outputs.


## [2026-05-12 21:10] 06-solution-validation

Completed final solution validation. The upgraded solution restores and builds successfully, no test projects were discovered, and framework-dependent publish validation passed for win-x86, win-x64, and win-arm64. Remaining warnings and runtime validation limitations were documented.

