# C# Modernisation Assessment

**Project:** TinyWall solution
**Solution:** `TinyWall.sln`
**Current version:** C# 14.0
**Target version:** C# 14.0
**Target framework:** .NET 10 (`net10.0-windows*`)
**Date:** 2026-05-15

## Summary

The solution already targets .NET 10 and explicitly sets `<LangVersion>14.0</LangVersion>` in all projects. This pass focuses on applying safe C# 14 syntax and enabling C# 14 analyser preferences.

| Category | Est. files | Method |
|----------|-----------:|--------|
| ⚠️ BREAKING CHANGES | 0 confirmed | Review only — no risky accessor `field` identifiers found |
| 🟢 ALWAYS-APPLY (dotnet format) | 15 | `dotnet format` for C# 14 analyzer rules |
| 🟢 ALWAYS-APPLY (LLM-only) | 4 | Manual `AsSpan()` simplification |
| 🟡 RECOMMEND | 0 planned | Extension blocks/partial constructors/operators skipped unless clear benefit appears |
| 🔴 OPT-IN (not applied) | N/A | None requested |

## Project language settings

| Project | Target framework | LangVersion | Nullable |
|---------|------------------|-------------|----------|
| `pylorak.Utilities/pylorak.Utilities.csproj` | `net10.0-windows` | `14.0` | `enable` |
| `pylorak.Windows/pylorak.Windows.csproj` | `net10.0-windows` | `14.0` | `enable` |
| `pylorak.Windows.Services/pylorak.Windows.Services.csproj` | `net10.0-windows` | `14.0` | `enable` |
| `pylorak.Windows.WFP/pylorak.Windows.WFP.csproj` | `net10.0-windows` | `14.0` | `enable` |
| `TinyWall/TinyWall.csproj` | `net10.0-windows10.0.26100.0` | `14.0` | `enable` |

## Phase 0: Breaking changes (must resolve first)

| Breaking change | C# Ver | Est. files | Severity | Mitigation |
|----------------|--------|-----------:|----------|------------|
| `field` keyword in property accessors | 14 | 0 confirmed | warning/error/behavior | Scan found only XML documentation text, not identifiers in property accessors. No change required. |
| `extension` contextual keyword | 14 | 1 file text matches | compile error if used as identifier in restricted contexts | Existing matches require review; no direct type/alias/type-parameter usage identified during initial scan. |
| Span overload resolution changes | 14 | ~5 | compile error/behavior | Build after changes; avoid removing `.AsSpan()` where overload choice could become ambiguous. |

## Phase 1: dotnet format (automated)

### `.editorconfig` additions

```ini
[*.cs]
csharp_style_prefer_unbound_generic_type_in_nameof = true:info
csharp_style_prefer_implicit_lambda = true:info
csharp_style_prefer_simplified_property_accessor = true:info
```

### Diagnostics to apply

| IDE Rule | Feature | Est. files | Tier |
|----------|---------|-----------:|------|
| IDE0340 | Unbound generic type in `nameof` | 0 | 🟢 |
| IDE0350 | Implicitly typed lambda parameters where modifiers are used | TBD | 🟢 |
| IDE0360 | Simplified property accessor using `field` | TBD | 🟢 |

### Command

```powershell
dotnet format TinyWall.sln --severity info --diagnostics IDE0340 IDE0350 IDE0360
```

## Phase 2: Manual C# 14 transformations

### 🟢 ALWAYS-APPLY (manual review)

| Feature | C# Ver | Est. files | Detection signal |
|---------|--------|-----------:|------------------|
| First-class Span types | 14 | ~5 | 20 `.AsSpan()` calls |
| Null-conditional assignment | 14 | up to ~33 | null checks followed by possible assignments |

## Phase 3: Opt-in (not applied unless requested)

| Feature | C# Ver | Impact | Notes |
|---------|--------|--------|-------|
| Extension blocks | 14 | API/source-shape churn | Skipped by default because existing static extension methods are stable and broad conversion is higher risk. |
| Partial events/constructors | 14 | Source-generator scenarios | Not applicable unless source-generation split declarations are introduced. |
| User-defined compound assignment operators | 14 | API/runtime semantics | Not applied without a clear performance-critical mutable numeric/container type. |

## Recommended execution order

0. Phase 0 review → build if needed
1. Phase 1 → build
2. Phase 2 manual changes → build/test
3. Update this assessment with execution results

## Execution results

### Phase 1 (dotnet format): Complete

- Added `.editorconfig` entries for C# 14 analyser preferences.
- Ran:
  ```powershell
  dotnet format .\TinyWall.sln --severity info --diagnostics IDE0340 IDE0350 IDE0360 --verbosity diagnostic
  ```
- Formatter completed successfully and formatted 15 files:
  - `TinyWall/Parser/ParserRegistryVariable.cs`
  - `TinyWall/UpdateChecker.cs`
  - `pylorak.Utilities/EventMerger.cs`
  - `pylorak.Windows.Services/DeviceInterfaceClass.cs`
  - `pylorak.Windows.WFP/Engine.cs`
  - `pylorak.Windows.WFP/NativeStructs.cs`
  - `pylorak.Windows.WFP/NetEventSubscription.cs`
  - `pylorak.Windows.WFP/PInvokeHelper.cs`
  - `pylorak.Windows/Hotkey.cs`
  - `pylorak.Windows/IconTools.cs`
  - `pylorak.Windows/NetStat/UdpTable.cs`
  - `pylorak.Windows/PathMapper.cs`
  - `pylorak.Windows/RegistryWatcher.cs`
  - `pylorak.Windows/TrafficRateMonitor.cs`
  - `pylorak.Windows/WinTrust.cs`

### Phase 2 (manual C# 14): Complete

| Feature | Files changed | Notes |
|---------|--------------:|-------|
| First-class `Span` conversions | 4 | Removed low-risk `.AsSpan()` calls where C# 14 implicit `string` → `ReadOnlySpan<char>` conversion preserves overload choice. |
| Null-conditional assignment | 0 | Reviewed candidates; skipped because matches were assignments to locals/fields or contained additional logic, not direct member assignment through a nullable receiver. |

Manual changes were applied in:
- `pylorak.Utilities/IpAddrMask.cs`
- `pylorak.Utilities/SpanUtils.cs`
- `pylorak.Windows/NetworkPath.cs`
- `pylorak.Windows/PathMapper.cs`

### Validation

- `run_build`: Build successful.
- `dotnet test .\TinyWall.sln --no-build --verbosity minimal`: Completed successfully with no reported test failures.

### Skipped (with reasons)

| Feature | Reason |
|---------|--------|
| Extension blocks | Recommend-tier API/source-shape rewrite with higher churn; no clear local benefit found. |
| User-defined compound assignment operators | No obvious mutable numeric/container type requiring in-place operator performance work. |
| Partial events/constructors | Source-generator-oriented feature; no matching pattern found. |

## Follow-up execution results: file-scoped namespaces and collection expressions

### Formatter pass: Complete

- Added `.editorconfig` entries:
  ```ini
  csharp_style_namespace_declarations = file_scoped:info
  dotnet_style_prefer_collection_expression = true:info
  ```
- Ran:
  ```powershell
  dotnet format .\TinyWall.sln --severity info --diagnostics IDE0161 IDE0300 IDE0301 IDE0302 IDE0303 IDE0304 IDE0305 IDE0306 --verbosity diagnostic
  ```
- Formatter completed successfully and formatted 37 files.
- Git-reported modified files after this pass:
  - `.editorconfig`
  - `TinyWall/AppFinderForm.cs`
  - `TinyWall/ApplicationExceptionForm.cs`
  - `TinyWall/ConnectionsForm.cs`
  - `TinyWall/Controller.cs`
  - `TinyWall/DatabaseClasses/AppDatabase.cs`
  - `TinyWall/DevelToolForm.cs`
  - `TinyWall/ExceptionPolicy.cs`
  - `TinyWall/Message.cs`
  - `TinyWall/Processes.cs`
  - `TinyWall/Program.cs`
  - `TinyWall/SerialisationHelper.cs`
  - `TinyWall/ServerState.cs`
  - `TinyWall/ServicePidMap.cs`
  - `TinyWall/Services.cs`
  - `TinyWall/TinyWallController.cs`
  - `TinyWall/TinyWallService.cs`
  - `TinyWall/Utils.cs`
  - `TinyWall/UwpPackagesForm.cs`
  - `TinyWall/WindowsFirewall.cs`
  - `pylorak.Utilities/CircularBuffer.cs`
  - `pylorak.Utilities/IpAddrMask.cs`
  - `pylorak.Windows.Services/ServiceBase.cs`
  - `pylorak.Windows.WFP/FilterCondition.cs`
  - `pylorak.Windows/PathMapper.cs`
  - `pylorak.Windows/TaskDialogue/TaskDialogue.cs`
  - `pylorak.Windows/TrafficRateMonitor.cs`

### Validation

- `run_build`: Build successful.
- `dotnet test .\TinyWall.sln --no-build --verbosity minimal`: Completed successfully with no reported test failures.
