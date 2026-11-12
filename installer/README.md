# Visual Studio MSI installer and application staging

`TinyWallSetup\TinyWallSetup.vdproj` is the sole installer project. `build-msi.ps1` stages both applications, refreshes that project's explicit file list and invokes Visual Studio Installer Projects to produce an x64 MSI. `build-package.ps1` only publishes and validates application staging archives.

## Requirements

- Windows, .NET 10 SDK, and Visual Studio MSBuild. TinyWall has COM references that `dotnet build`/`dotnet publish` cannot resolve; the packaging script uses MSBuild for TinyWall and `dotnet publish` for Modern TinyWall.
- Windows SDK/COM type libraries required by TinyWall's `NetFwTypeLib` and Task Scheduler references.
- The Visual Studio Installer Projects extension is required for MSI compilation. MSBuild and `dotnet build` cannot compile a `.vdproj` project. The scripts do not install tools or extensions.

The staging script publishes both apps self-contained for the selected Windows runtime. It uses isolated `artifacts\build\<runtime>` build/intermediate directories, not the normal Release outputs (which may be locked by a running UI). It checks that both application versions and the MSI project's `ProductVersion` match. The application version determines the archive filename; the MSI version is maintained in the Visual Studio project.

TinyWall currently resolves newer `System.CodeDom`, `System.Collections.Immutable`, and `System.Reflection.Metadata` packages than the framework copies used by Modern TinyWall. `Packaging.Dependencies.targets` explicitly adds the versions from TinyWall's resolved dependency manifest to Modern's packaging build/restore graph. This is not a DLL replacement: both applications are built against the aligned dependencies, and the final merge still rejects every differing duplicate. The same targets preserve the generated WinUI `.pri` and `.xbf` resources that the plain publish pipeline otherwise omits.

## Build the MSI

```powershell
# Publish, verify, refresh the setup project and compile an x64 MSI.
.\installer\build-msi.ps1

# Rebuild using an already-published, matching and verified payload.
.\installer\build-msi.ps1 -UsePublishedFiles
```

The output is `TinyWallSetup\Release\TinyWallSetup.msi` with a SHA-256 sidecar. The isolated `TinyWallSetup\TinyWallSetup.sln` avoids rebuilding unrelated solution projects. After staging, the same `.vdproj` can also be opened and built in Visual Studio. Both applications are self-contained; no .NET runtime prerequisite or bootstrapper download is needed. The setup project targets x64 only, even when the archive script publishes other architectures.

The project's post-build step uses the Windows Installer API (`complete-msi.ps1`) to add elevated, deferred service actions to the MSI produced by Visual Studio; it is not another installer toolchain. Installation invokes the packaged `TinyWall.exe /install`. Uninstallation invokes `/uninstall /quiet` before file removal, using TinyWall's own authenticated service-stop and WFP cleanup rather than an SCM stop that the Release service deliberately rejects. Quiet uninstallation requires administrator privileges and rejects a locked service without displaying an inaccessible password prompt; unlock TinyWall before uninstalling. A rollback action cleans up a failed first installation. Existing ProgramData settings and catalogue files are not MSI payload files.

`test-msi.ps1` opens the MSI read-only and checks its file count, destination paths and sizes against the staged payload, required application/runtime/catalogue files, and service action ordering. These are build checks, not proof of installation behaviour. Install, upgrade, failure recovery and uninstall still require the VM checklist below. The build scripts do not install tools, change registry settings, run the MSI or start either application on the build host.

**VM validation status:** the 4.5.7 x64 MSI installed in the Windows 11 Hyper-V development VM, but its service failed with a stack overflow in IP address parsing. Version 4.5.8 fixed the parser, but its upgrade omitted two UI libraries. Version 4.5.9 corrects the installer metadata; live validation remains necessary before distribution.

## Publish and validate application staging archives

Run these commands from the repository root:

```powershell
# Offline tests: sole MSI project/version, incomplete inputs, collisions, archive integrity.
.\installer\build-package.ps1 -SelfTest

# Publish both apps, validate shared-file collisions, combine outputs, and create archives.
.\installer\build-package.ps1 -Architecture All

# Publish just one architecture; this does not select the MSI target platform.
.\installer\build-package.ps1 -Architecture win-x64

# Validate already-published inputs without building or changing them.
.\installer\build-package.ps1 -Architecture win-x64 -ValidateOnly

# Read-only verification of the completed ZIP, manifest hashes, and checksum sidecar.
.\installer\build-package.ps1 -Architecture win-x64 -VerifyArchive
```

`All` supports `win-x86`, `win-x64`, and `win-arm64` application archives; any other value fails explicitly. This does not imply matching MSI platform support or change the Visual Studio setup project's target platform. Architecture-specific publish or COM resolution failures stop packaging for that runtime. Shared relative filenames are compared by SHA-256 before combining: identical files are retained once, while differing files stop the build instead of being overwritten.

The application inputs are written to `artifacts\publish\TinyWall\<runtime>` and `artifacts\publish\ModernTinyWall\<runtime>`. Combined files go to `artifacts\publish\TinyWallDual\<runtime>` and archives to `artifacts\release\TinyWall-<version>-<runtime>.zip`. Each archive contains `package-manifest.json` with the release version, runtime, and every payload file's size and SHA-256. A sibling `.zip.sha256` records the archive hash. Packaging verifies the full archive before reporting success. These checks detect corruption, not authenticity; no signing is performed. CI currently publishes these ZIP artefacts, not MSI installers.

Packaging builds files and invokes only the freshly published legacy executable's explicit offline compiler:

```powershell
# Invoked automatically by build-package.ps1, not a service/UI command.
.\artifacts\publish\TinyWall\win-x64\TinyWall.exe /buildcatalogue `
    .\TinyWallSetup\Sources\CommonAppData\TinyWall\profiles.json `
    .\artifacts\catalogue\profiles.db
```

The compiler runs before service, UI, application-data or Windows-setting initialisation and converts only the specified repository files. A native-host compiler is published separately when cross-packaging another architecture. The non-empty source catalogue is compiled and validated by the application, then the SQLite header, TinyWall application ID/schema, manifest application count and payload hashes are checked during packaging. The required read-only seed is staged as `catalogue\profiles.db` beneath the combined application directory, never directly into ProgramData.

The service owns the authoritative `%ProgramData%\TinyWall\profiles.db`. On first startup, if this DB is absent, it imports an existing `%ProgramData%\TinyWall\profiles.json` without modifying that JSON; otherwise it initialises from the protected package seed. An existing DB remains authoritative. Settings remain JSON. Catalogue migration is service-owned; verify that the MSI preserves existing ProgramData settings and catalogue files.

Staging and MSI compilation must not launch either UI, install a service, run an installer or change Windows configuration. Archives are staging/review artefacts, not portable service installations. Release service authentication requires matching `TinyWall.exe` and `ModernTinyWall.exe` in the same administrator-protected installation directory; copying them into a user-writable directory is insufficient. Driver files are excluded from staging.

## Windows 11 Hyper-V manual test checklist (VM only)

Use a disposable Windows 11 Hyper-V VM with a checkpoint and a clean snapshot. Never run these installer/service tests on a developer or CI host.

1. **Secure install:** Copy the architecture-matching MSI into the VM from a verified release source. Signing is not provided by the staging workflow; do not treat publisher metadata as a digital signature. Verify administrator elevation, an administrator-protected Program Files location and rejection of user-writable installation paths. Confirm a standard user cannot replace either executable or its dependencies, both executables are in the same directory and no driver was installed.
2. **Both clients communicate and preview UI:** Start TinyWall and Modern TinyWall separately. Confirm each reads service status/settings and can submit a harmless setting change; verify the other client observes the resulting state. In the VM, launch Modern TinyWall with `--preview-theme=light`, `--preview-theme=dark`, and `--preview-theme=system`; also verify `--preview-reduced-motion` and `--preview-size=1024x768` (these flags are per-process previews and should not change Windows settings). Verify the opening window is centred within the monitor work area, remains inside the taskbar margins, and does not jump off-screen after first-show DPI adjustment. Record any connection, compatibility, or rendering errors.
3. **Service lifecycle:** Confirm the service is installed and running after setup, survives a VM reboot and stops/removes cleanly during uninstall. Confirm neither client is left running against a removed service.
4. **Catalogue migration:** In separate disposable VM checkpoints, test no previous catalogue (DB initialised from the protected seed), legacy `profiles.json` only (one-time import, original JSON preserved byte-for-byte) and an existing `profiles.db` (remains authoritative after reinstall). Verify settings JSON remains unchanged, both clients show matching catalogue applications/policies, the Program Files seed is not writable by a standard user and uninstall does not remove existing ProgramData settings/catalogue files. In the VM only, exercise an update containing an invalid catalogue/schema and verify rejection retains the previous working DB.
5. **ETW:** Exercise the UI's connection/activity view and verify expected ETW-backed data appears while the service is running; note behaviour when the service is stopped.
6. **Firewall decisions:** In this isolated VM, verify a known allowed application remains usable, a test application follows the selected block/allow decision and changing the decision is reflected by both clients. Do not test against production networks or critical host traffic.
7. **Uninstall and rollback:** Take a second checkpoint before uninstall. Uninstall normally, confirm service removal and expected application/shortcut cleanup, then restore the checkpoint and repeat the install/uninstall path. Test interrupted or failed uninstallation only in the disposable VM and restore the checkpoint afterwards.

The checklist is manual and VM-only; package validation does not install or exercise Windows services, ETW, firewall rules, or either UI.
