# 06.01-inno-setup-installer Progress Detail

## Changes

- Added `installer/TinyWall.iss` as the Inno Setup packaging entry point.
- Added `installer/README.md` with publish and installer build instructions for:
  - `win-x86`
  - `win-x64`
  - `win-arm64`
- Saved the decision that Inno Setup is now the preferred installer option.
- Extended the upgrade plan with post-upgrade Inno Setup and warning remediation work.

## Validation

- Confirmed existing architecture publish directories under `artifacts/publish/TinyWall`.
- The Inno Setup compiler was not invoked in this step because the environment/tool availability was not confirmed.

## Notes

The Visual Studio setup project remains in the repository for reference, but it is no longer the primary installer path for the .NET 10 upgrade.
