# .NET Version Upgrade

## Strategy
**Selected**: Hybrid
**Rationale**: The solution has five SDK-style .NET projects with uneven risk. `pylorak.Utilities`, `pylorak.Windows`, and `pylorak.Windows.Services` form a dependency chain, while `pylorak.Windows.WFP` and `TinyWall` are independent top-level projects. TinyWall contains most compatibility findings, so it should be handled as a focused application group after lower-risk dependencies are stable.

### Execution Constraints
- Execute groups in dependency order: foundation libraries first, independent Windows filtering library next, TinyWall application after reusable libraries are stable.
- Validate each group before starting dependent groups.
- Treat `TinyWallSetup.vdproj` as packaging review work, not a normal SDK-style .NET target framework upgrade.
- Preserve x86, x64, and ARM support when changing build, runtime, and packaging settings.
- Keep implementation changes blocked until the user reviews and approves the plan.

## Preferences
- **Flow Mode**: Guided
- **Commit Strategy**: After Each Phase
- **Pace**: Methodical
- **Target Framework**: .NET 10 (`net10.0`)
- **Architecture Target**: x86, x64, ARM
- **Operating System Target**: Windows 10 and Windows 11 only
- **Language Style**: British English everywhere, including code, comments, identifiers, documentation, and user-facing text
- **Change Control**: Do not make implementation repository changes until the user has reviewed and approved the plan
- **Source Branch**: `master`
- **Working Branch**: `upgrade-to-NET10`

## Decisions
- Create assessment and plan only before implementation — user requested plan review before repository changes.
- Target .NET 10 LTS and all CPU architectures: x86, x64, ARM.
- Source branch corrected to `master` — user clarified the initial source branch.
- Hybrid strategy selected — assessment shows a small dependency chain, independent Windows-facing projects, and a concentrated TinyWall application compatibility workload.
- Automatic commits enabled — user approved continuing the upgrade with commits after each phase.
- Windows 10 and Windows 11 are the intentional operating system targets, so Windows-specific .NET 10 target frameworks are acceptable.

## Custom Instructions
<!-- Task-specific overrides: "For {taskId}: {instruction}" -->
