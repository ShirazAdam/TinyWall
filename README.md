## Please note that this is still an experimental work in progress. Modern TinyWall runs on WinUI 3/.NET 10, along with TinyWall that runs on WinForms/.NET 10.

[![.NET 10 Desktop](https://github.com/ShirazAdam/TinyWall/actions/workflows/dotnet-desktop.yml/badge.svg?branch=upgrade-to-NET10)](https://github.com/ShirazAdam/TinyWall/actions/workflows/dotnet-desktop.yml)

[![Automatic Dependency Submission](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependency-graph/auto-submission/badge.svg)](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependency-graph/auto-submission)

[![CodeQL](https://github.com/ShirazAdam/TinyWall/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/ShirazAdam/TinyWall/actions/workflows/github-code-scanning/codeql)

[![Dependabot Updates](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependabot/dependabot-updates/badge.svg)](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependabot/dependabot-updates)

# TinyWall / Modern TinyWall

TinyWall is a free, lightweight and non-intrusive firewall for Windows. It is designed to harden the built-in Windows Firewall while keeping day-to-day use simple: no noisy pop-ups, no bundled drivers, and no unnecessary background clutter.

The original project suffered from a lack of updates and support. The original project also suffers from performance problems where right-clicking on the tray icon can take anywhere between 2 - 12 seconds to show the context menu. Some forms can also freeze because expensive work runs on the UI thread. This fork aims to experiment and continue the development of TinyWall, keeping it up-to-date and compatible with the latest Windows versions, while also adding new features and improvements.

This repository now contains both the legacy TinyWall application and a new **Modern TinyWall** application. Modern TinyWall is a .NET 10 Windows desktop client that modernises the user experience while reusing the existing firewall, Windows integration, package discovery, service-control, and Windows Filtering Platform functionality where practical. The goal is to keep the proven TinyWall behaviour while improving responsiveness, maintainability, and compatibility with current Windows and .NET releases.

The applications provide tray-based control for managing firewall modes, application exceptions, service and process rules, Microsoft Store / packaged app rules, connection activity, blocklists, and firewall log entries. They help users allow trusted applications, block unwanted network access, and review network activity without needing to edit Windows Firewall rules manually.

TinyWall and Modern TinyWall focus on being practical and low-overhead. They use Windows networking and security features rather than replacing them, and aim to keep the user interface responsive while doing heavier discovery and rule-management work in the background.

Now enabled to run natively on x86, x64, and ARM.

## Current development focus

- Targeting .NET 10 for the actively modernised projects.
- Adding the new `Modern TinyWall` desktop client alongside the existing `TinyWall` application.
- Moving expensive UI-triggered operations off the UI thread where possible.
- Improving tray responsiveness and modernising tray command handling.
- Preserving existing TinyWall firewall behaviour while modernising the surrounding application code.
- Keeping native Windows interop explicit and source-generator friendly for modern .NET builds.

You're welcome to hack and slash at it. Enjoy!

#### Hosted on

 - GitHub -> <https://github.com/ShirazAdam/Tinywall>

## How to build

### Necessary tools

- [Microsoft Visual Studio 2026](https://visualstudio.microsoft.com/vs/)
- [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Microsoft Visual Studio 2022/2026 Installer Project Extension](https://marketplace.visualstudio.com/items?itemName=VisualStudioClient.MicrosoftVisualStudio2022InstallerProjects)

### To build the application

1. Open the solution file in Visual Studio.
1. Build `Modern TinyWall` to run the modernised .NET 10 client.
1. Build `TinyWall` to run the legacy WinForms application.
1. The referenced projects are built automatically by Visual Studio as needed.
1. Done.

### Main projects

- `ModernTinyWall` - modern .NET 10 Windows desktop client.
- `TinyWall` - original / legacy TinyWall application.
- `TinyWall.Core` - shared core functionality.
- `ModernTinyWall.Windows` - Windows platform helpers and native interop wrappers.
- `ModernTinyWall.Windows.WFP` - Windows Filtering Platform integration.
- `ModernTinyWall.Windows.Services` - Windows service-control helpers.
- `ModernTinyWall.Utilities` - shared utility code for the modernised projects.

### To update/build build the database of known applications

1. Adjust the individual JSON files in the `TinyWall\Database` folder.
1. Start the application with the `/develtool` flag.
1. Use the `Database creator` tab to create one combined database file in JSON format. The output file will be called `profiles.json`.
1. To use the new database in debug builds, copy the output file to the `TinyWall\bin\Debug` folder.
1. Done.

## Contributing

Please feel free to contribute to the project. Fork the repository, make your changes and submit a pull request. All contributions are welcome, whether it's fixing bugs, adding new features, improving documentation, or anything else you think would benefit the project.

## Licence

- Task Dialogue wrapper (code in directory `pylorak.Windows\TaskDialog`) written by KevinGre ([link](https://www.codeproject.com/Articles/17026/TaskDialog-for-WinForms)) and placed under Public Domain.

- All other code and artefacts in the repository are under the DO WHAT THE F*** YOU WANT TO PUBLIC LICENCE. See `LICENCE.txt` for more information.
