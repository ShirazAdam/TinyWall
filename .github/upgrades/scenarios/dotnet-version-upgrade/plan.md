# .NET Version Upgrade Plan

## Overview

**Target**: Upgrade the TinyWall solution from .NET Framework 4.8.1 to .NET 10, using `net10.0` for non-desktop libraries and `net10.0-windows` where Windows desktop APIs require it.
**Scope**: Five SDK-style .NET projects plus the existing setup project in the solution. The assessment found a small dependency chain, several independent Windows-facing projects, package updates/removals, and a concentrated compatibility workload in the TinyWall WinForms application.

### Selected Strategy

**Hybrid** — Solution segmented into project groups with per-group strategies.
**Rationale**: The solution has a small dependency chain and very uneven risk. `pylorak.Utilities`, `pylorak.Windows`, and `pylorak.Windows.Services` form a dependency sequence, while `pylorak.Windows.WFP` and `TinyWall` are independent top-level projects. TinyWall carries most compatibility issues and needs separate treatment from the lower-risk libraries.

### Group Definitions

- **Foundation libraries**: `pylorak.Utilities`, `pylorak.Windows`, `pylorak.Windows.Services` using bottom-up ordering.
- **Independent Windows filtering library**: `pylorak.Windows.WFP` using an all-at-once project upgrade.
- **TinyWall desktop application**: `TinyWall` using a focused application upgrade after shared dependencies are stable.
- **Installer project**: `TinyWallSetup.vdproj` reviewed separately because Visual Studio setup projects do not target modern .NET in the same way as SDK-style projects.

### Group Execution Order

1. Upgrade and validate the foundation libraries in dependency order.
2. Upgrade and validate `pylorak.Windows.WFP` because it is independent of the foundation chain.
3. Upgrade the `TinyWall` application after reusable libraries are stable.
4. Review installer compatibility after the application output shape is known.
5. Run full-solution validation across all upgraded projects.

## Tasks

### 01-sdk-environment

Validate that the required .NET 10 SDK is available, confirm whether any `global.json` constrains SDK selection, and establish the target framework mapping for each project. Non-desktop libraries should move to `net10.0`; WinForms or Windows desktop projects should move to `net10.0-windows` with Windows desktop support enabled. CPU architecture support must be preserved for x86, x64, and ARM.

**Done when**: The SDK and framework targeting decisions are documented, any SDK constraints are identified, and the architecture support approach is clear before project changes begin.

---

### 02-foundation-libraries

Upgrade the dependency chain made up of `pylorak.Utilities`, `pylorak.Windows`, and `pylorak.Windows.Services`. This group starts with `pylorak.Utilities`, then moves to `pylorak.Windows`, and finishes with `pylorak.Windows.Services` so that each dependent project builds against an already-upgraded foundation.

Key concerns include removing framework-included packages where appropriate, preserving Windows Forms and GDI+ behaviour in `pylorak.Windows`, and replacing or removing unsupported Code Access Security patterns.

**Done when**: The three foundation projects target their .NET 10 frameworks, required package changes are applied, source compatibility issues are resolved, and the group builds successfully in dependency order.

---

### 03-windows-filtering-library

Upgrade `pylorak.Windows.WFP` to .NET 10. This project is independent in the assessment graph, so it can be handled separately after the foundation plan is established. The main concerns are source compatibility issues, behavioural changes, and legacy cryptography usage.

**Done when**: `pylorak.Windows.WFP` targets .NET 10, compatibility issues identified by the assessment are addressed, and the project builds successfully.

---

### 04-tinywall-application

Upgrade the `TinyWall` WinForms application to .NET 10 for Windows desktop. This is the main application workload and contains the majority of compatibility findings, especially Windows Forms, GDI+ / System.Drawing, legacy configuration, configuration installation components, WMI, Code Access Security, Windows ACLs, and legacy controls.

The task includes updating Microsoft.Extensions packages to their .NET 10 versions, removing packages whose functionality is included by the target framework, preserving Windows desktop behaviour, and ensuring application settings and runtime assumptions still work under modern .NET.

**Done when**: `TinyWall` targets `net10.0-windows`, its package references are compatible with .NET 10, mandatory API issues are resolved, relevant behavioural changes are reviewed, and the application builds successfully.

---

### 05-installer-packaging

Review `TinyWallSetup.vdproj` after the application upgrade because setup projects are not SDK-style .NET projects and may need output path, runtime, architecture, or prerequisite adjustments. The review should ensure the installer can package the .NET 10 application output for x86, x64, and ARM as required.

**Done when**: Installer compatibility is documented, packaging assumptions are updated if needed, and the chosen deployment approach is confirmed for all target CPU architectures.

---

### 06-solution-validation

Run full validation after all upgrade groups are complete. This includes restore, build, targeted tests if available, application smoke checks, and architecture-specific verification for x86, x64, and ARM outputs.

**Done when**: The upgraded solution restores and builds successfully, available tests pass or are documented, smoke checks are completed, and any remaining warnings or known limitations are recorded.
