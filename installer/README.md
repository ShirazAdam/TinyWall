# TinyWall dual-application installer

The installer packages both the legacy **TinyWall** client and **Modern TinyWall** beside each other in one protected Program Files directory. Modern TinyWall depends on `TinyWall.exe` being colocated with it to communicate with the service. No driver is packaged.

## Requirements

- Windows, .NET 10 SDK, and Visual Studio MSBuild. TinyWall has COM references that `dotnet build`/`dotnet publish` cannot resolve; the packaging script uses MSBuild for TinyWall and `dotnet publish` for Modern TinyWall.
- Windows SDK/COM type libraries required by TinyWall's `NetFwTypeLib` and Task Scheduler references.
- Inno Setup 6 (`ISCC.exe`) only to compile the installer. If it is unavailable on `PATH` or in the usual `Program Files` locations, packaging still creates the dual-application archives and reports that no installer was built. The script does not install tools.

Both apps are published self-contained for the selected Windows runtime, so the target does not need the .NET Desktop Runtime installed. Packaging uses isolated `artifacts\build\<runtime>` build/intermediate directories, not the normal Release outputs (which may be locked by a running UI). The script checks that both project versions match and derives installer/archive versioning from those project files.

TinyWall currently resolves newer `System.CodeDom`, `System.Collections.Immutable`, and `System.Reflection.Metadata` packages than the framework copies used by Modern TinyWall. `Packaging.Dependencies.targets` explicitly adds the versions from TinyWall's resolved dependency manifest to Modern's packaging build/restore graph. This is not a DLL replacement: both applications are built against the aligned dependencies, and the final merge still rejects every differing duplicate. The same targets preserve the generated WinUI `.pri` and `.xbf` resources that the plain publish pipeline otherwise omits.

## Build and validate

Run these commands from the repository root:

```powershell
# Offline tests: versions, incomplete inputs, collisions, archive integrity/tampering.
.\installer\build-package.ps1 -SelfTest

# Publish both apps, validate shared-file collisions, combine outputs, and create archives.
# Build an Inno installer too only when ISCC.exe is already available.
.\installer\build-package.ps1 -Architecture All

# Build just one architecture.
.\installer\build-package.ps1 -Architecture win-x64

# Validate already-published inputs without building or changing them.
.\installer\build-package.ps1 -Architecture win-x64 -ValidateOnly

# Read-only verification of the completed ZIP, manifest hashes, and checksum sidecar.
.\installer\build-package.ps1 -Architecture win-x64 -VerifyArchive
```

`All` supports `win-x86`, `win-x64`, and `win-arm64`; any other value fails explicitly. The x86 installer permits x86/x64 Windows, while the x64 and arm64 installers are restricted to their matching architecture. Architecture-specific publish or COM resolution failures stop packaging for that runtime. Shared relative filenames are compared by SHA-256 before combining: identical files are retained once, while differing files stop the build instead of being overwritten.

The application inputs are written to `artifacts\publish\TinyWall\<runtime>` and `artifacts\publish\ModernTinyWall\<runtime>`. Combined files go to `artifacts\publish\TinyWallDual\<runtime>`, archives to `artifacts\release\TinyWall-<version>-<runtime>.zip`, and available Inno installers to `artifacts\installer\<runtime>`. Each archive contains `package-manifest.json` with the release version, runtime, and every payload file's size and SHA-256. A sibling `.zip.sha256` records the archive hash. Packaging verifies the full archive before reporting success. These checks detect corruption, not authenticity; no signing is performed.

Packaging builds files and invokes only the freshly published legacy executable's explicit offline compiler:

```powershell
# Invoked automatically by build-package.ps1, not a service/UI command.
.\artifacts\publish\TinyWall\win-x64\TinyWall.exe /buildcatalogue `
    .\TinyWallSetup\Sources\CommonAppData\TinyWall\profiles.json `
    .\artifacts\catalogue\profiles.db
```

The compiler runs before service, UI, application-data, or Windows-setting initialization and converts only the specified repository files. A native-host compiler is published separately when cross-packaging another architecture. The nonempty source catalogue is compiled and validated by the application, then the SQLite header, TinyWall application ID/schema, manifest application count, and payload hashes are checked during packaging. The required read-only seed is staged as `catalogue\profiles.db` beneath the combined installation directory, never directly into ProgramData.

The service owns the authoritative `%ProgramData%\TinyWall\profiles.db`. On first startup, if this DB is absent, it imports an existing `%ProgramData%\TinyWall\profiles.json` without modifying that JSON; otherwise it initializes from the protected package seed. An existing DB remains authoritative. Settings remain JSON. The installer does not copy, overwrite, migrate, or remove ProgramData catalogue/settings files; migration is service-owned.

Packaging does not launch either UI, install a service, run an installer, or change Windows configuration. The service install and uninstall commands are installer actions and run only when an end user executes the installer. The installer requests administrator elevation and restricts installation to its default Program Files directory, rejecting custom `/DIR` locations. Archives are staging/review artifacts, not portable service installations; copying them into a user-writable directory does not satisfy Release service authentication.

## Windows 11 Hyper-V manual test checklist (VM only)

Use a disposable Windows 11 Hyper-V VM with a checkpoint and a clean snapshot. Never run these installer/service tests on a developer or CI host.

1. **Secure install:** Copy the architecture-matching installer into the VM from a verified release source. This workflow does not sign installers, so expect an unsigned-publisher UAC warning; do not treat the embedded publisher text as a digital signature. Install to the default Program Files location and verify a custom user-writable `/DIR` is rejected. Confirm a standard user cannot replace either executable or its dependencies, both executables are in the same directory, and no driver was installed.
2. **Both clients communicate and preview UI:** Start TinyWall and Modern TinyWall separately. Confirm each reads service status/settings and can submit a harmless setting change; verify the other client observes the resulting state. In the VM, launch Modern TinyWall with `--preview-theme=light`, `--preview-theme=dark`, and `--preview-theme=system`; also verify `--preview-reduced-motion` and `--preview-size=1024x768` (these flags are per-process previews and should not change Windows settings). Verify the opening window is centred within the monitor work area, remains inside the taskbar margins, and does not jump off-screen after first-show DPI adjustment. Record any connection, compatibility, or rendering errors.
3. **Service lifecycle:** Confirm the service is installed and running after setup, survives a VM reboot, and stops/removes cleanly during uninstall. Confirm neither client is left running against a removed service.
   - **Catalogue migration:** In separate disposable VM checkpoints, test no previous catalogue (DB initialized from the protected seed), legacy `profiles.json` only (one-time import, original JSON preserved byte-for-byte), and an existing `profiles.db` (remains authoritative after reinstall). Verify settings JSON remains unchanged, both clients show matching catalogue applications/policies, the Program Files seed is not writable by a standard user, and uninstall does not remove existing ProgramData settings/catalogue files. In the VM only, exercise an update containing an invalid catalogue/schema and verify rejection/rollback retains the previous working DB and both clients still see the previous catalogue.
4. **ETW:** Exercise the UI's connection/activity view and verify expected ETW-backed data appears while the service is running; note behavior when the service is stopped.
5. **Firewall decisions:** In this isolated VM, verify a known allowed application remains usable, a test application follows the selected block/allow decision, and changing the decision is reflected by both clients. Do not test against production networks or critical host traffic.
6. **Uninstall and rollback:** Take a second checkpoint before uninstall. Uninstall normally, confirm service removal and expected application/shortcut cleanup, then restore the checkpoint and repeat the install/uninstall path. If testing an interrupted or failed uninstall, do so only in the disposable VM and restore the checkpoint afterward.

The checklist is manual and VM-only; package validation does not install or exercise Windows services, ETW, firewall rules, or either UI.
