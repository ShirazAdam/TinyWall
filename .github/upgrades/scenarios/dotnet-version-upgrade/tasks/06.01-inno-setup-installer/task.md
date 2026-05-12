# 06.01-inno-setup-installer: Create Inno Setup installer path

## Objective
Replace the previous Visual Studio setup project direction with an Inno Setup packaging approach.

## Scope
- Inspect publish output and application metadata.
- Create or update an Inno Setup script for TinyWall.
- Document architecture assumptions for x86, x64, and ARM64.
- Keep Windows 10 and Windows 11 as the intended operating system targets.

## Done When
An Inno Setup packaging entry point exists and the old `.vdproj` path is no longer the primary installer option.
