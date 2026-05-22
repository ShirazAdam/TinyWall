# Copilot Instructions

## General Guidelines
- Use British English for all communication and code/user-facing text changes in this codebase; convert non-British English spelling to British English where practical.

## Azure Rules
- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool ask the user to enable it.

## Performance and Responsiveness
- For TinyWall performance optimisation, start analysis from the current codebase state (do not rely on prior-phase changes, which may have been removed); delegate non-UI work to background threads and apply async/await where practical to improve UI rendering and responsiveness
- When async/UI responsiveness is selected as a modernisation priority, prioritise diagnosing UI thread bottlenecks, convert blocking work to async/background tasks, apply async/await patterns safely, and verify improvements with UI responsiveness tests and benchmarks. Make small, focused commits and ensure all tests pass before merging.
- Move heavy work in ProcessesForm.UpdateListAsync and ServicesForm.UpdateListAsync off the UI thread: identify blocking operations, offload non-UI work to background threads or tasks, apply async/await safely, ensure thread-safety for UI updates (marshal to the UI thread), and verify improvements with responsiveness tests and benchmarks.

## Code Modernisation
- Apply file-scoped namespaces and collection expression modernisations when safe.
- Ensure safety by verifying the project targets a compatible language version, the change does not alter public APIs or runtime behaviour, and all tests pass before committing.
- Prefer small, focused commits or PRs for modernisation changes to ease review and rollback
- Replace DllImport usages with the LibraryImport source-generator modernisation where safe and beneficial:
  - Verify target framework and language support (e.g., .NET version that supports LibraryImport).
  - Preserve calling conventions, SetLastError semantics, and marshaling behaviour; ensure parameter and return types are blittable or explicitly marshalable.
  - Confirm no change in public API or observable runtime behaviour.
  - Run unit tests and performance benchmarks to validate correctness and measure improvements.
  - Apply changes in small, reviewable commits and coordinate with the async/UI responsiveness work when both are performed.

### Modernisation Priorities
- When proceeding from a list of modernisation opportunities and the user selects both high-priority items (async/UI responsiveness and DllImport → LibraryImport), implement both. Prioritise improving UI responsiveness first (diagnose UI-blocking work, offload to background threads, apply async/await — specifically move heavy work in ProcessesForm.UpdateListAsync and ServicesForm.UpdateListAsync off the UI thread), then modernise P/Invoke to LibraryImport, coordinating changes, running tests and benchmarks, and keeping commits small and reversible
