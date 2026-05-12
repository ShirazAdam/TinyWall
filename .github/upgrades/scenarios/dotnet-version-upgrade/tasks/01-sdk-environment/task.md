# 01-sdk-environment: SDK Environment

Validate that the required .NET 10 SDK is available, confirm whether any `global.json` constrains SDK selection, and establish the target framework mapping for each project. Non-desktop libraries should move to `net10.0`; WinForms or Windows desktop projects should move to `net10.0-windows` with Windows desktop support enabled. CPU architecture support must be preserved for x86, x64, and ARM.

**Done when**: The SDK and framework targeting decisions are documented, any SDK constraints are identified, and the architecture support approach is clear before project changes begin.
