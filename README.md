### Please note that this is an experimental work in progress. Modern TinyWall runs on WinUI 3/.NET 10, along with TinyWall that runs on WinForms/.NET 10. The application responsiveness of Modern TinyWall already surpasses legacy TinyWall.

[![.NET 10 Desktop](https://github.com/ShirazAdam/TinyWall/actions/workflows/dotnet-desktop.yml/badge.svg?branch=upgrade-to-NET10)](https://github.com/ShirazAdam/TinyWall/actions/workflows/dotnet-desktop.yml)

[![Automatic Dependency Submission](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependency-graph/auto-submission/badge.svg)](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependency-graph/auto-submission)

[![CodeQL](https://github.com/ShirazAdam/TinyWall/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/ShirazAdam/TinyWall/actions/workflows/github-code-scanning/codeql)

[![Dependabot Updates](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependabot/dependabot-updates/badge.svg)](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependabot/dependabot-updates)

# TinyWall / Modern TinyWall

<img width="1412" height="816" alt="image" src="https://github.com/user-attachments/assets/e373c109-fa36-40af-8acf-907d57264af5" />

TinyWall is a free, lightweight and non-intrusive firewall for Windows. It is designed to harden the built-in Windows Firewall while keeping day-to-day use simple: no bundled drivers and no unnecessary background clutter. Modern TinyWall can show an optional decision prompt when it detects a blocked outbound connection from an application that has not already been approved.

The original project suffered from a lack of updates and support. The original project also suffers from performance problems where right-clicking on the tray icon can take anywhere between 2 - 12 seconds to show the context menu. Some forms can also freeze because expensive work runs on the UI thread. This fork aims to experiment and continue the development of TinyWall, keeping it up-to-date and compatible with the latest Windows versions, while also adding new features and improvements.

This repository now contains both the legacy TinyWall application and a new **Modern TinyWall** application. Modern TinyWall is a .NET 10 Windows desktop client that modernises the user experience while reusing the existing firewall, Windows integration, package discovery, service-control, and Windows Filtering Platform functionality where practical. The goal is to keep the proven TinyWall behaviour while improving responsiveness, maintainability, and compatibility with current Windows and .NET releases.

The applications provide tray-based control for managing firewall modes, application exceptions, service and process rules, Microsoft Store / packaged app rules, connection activity, blocklists, and firewall log entries. They help users allow trusted applications, block unwanted network access, and review network activity without needing to edit Windows Firewall rules manually.

Modern TinyWall uses WinUI 3 dialogs. The legacy WinForms application uses the built-in .NET Windows Task Dialog API, replacing the former third-party wrapper while retaining its prompts, command-link choices, expandable details, progress indicators, and cancellation options.

Modern TinyWall uses WinUI's animated sidebar selection indicator and fades between views. Navigation animations follow the Windows animation setting and do not run while the window is hidden or minimised.

The Overview traffic chart uses smooth curves through the recorded download and upload samples, without overshooting their values.

Modern TinyWall's outbound connection prompt displays the application's name, icon, executable path, protocol, and destination. Users can allow the destination for five minutes, always allow the application, deny the attempt, or always block the application. The initial connection attempt is blocked before the prompt appears, so the application may need to retry after it is allowed. Existing approved rules continue to work without prompting.

TinyWall and Modern TinyWall focus on being practical and low-overhead. They use Windows networking and security features rather than replacing them, and aim to keep the user interface responsive while doing heavier discovery and rule-management work in the background.

Now enabled to run natively on x86, x64, and ARM.

## Application data usage

Modern TinyWall's **Data usage** view, immediately after Overview, lists applications with recorded TCP/UDP traffic. It shows Total, Internet incoming/outgoing, and Local incoming/outgoing bytes, with search, sorting, and Today, Last 7 days, Last 30 days, and All time filters. The last-7/30-day filters include today and use the machine's local calendar days. Totals combine multiple instances of the same executable path and remain visible after those applications exit.

The **updated TinyWall service must be installed and running**. Its privileged Windows ETW collector runs independently of the UI; the UI does not need administrator access just to read usage. Daily aggregates are committed to SQLite at `%ProgramData%\TinyWall\data-usage.db` every five seconds and on normal service shutdown. No packet contents or remote-address history are stored. History begins when collection starts, not retroactively. A crash or forced shutdown can lose the most recent uncommitted batch.

Local means private, loopback, link-local, IPv6 unique/site-local, shared-address (`100.64.0.0/10`), and multicast/broadcast addresses. Public remote addresses count as Internet. This is address-based, so private-address VPN traffic counts as Local and VPN/tunnel overhead may be attributed separately. ETW TCP/UDP byte sizes are not billing-grade interface counters: they do not include all link-layer overhead, retransmission events, or other protocols. Loopback traffic can count on both the sending and receiving application.

Shared hosts such as `svchost.exe` are grouped by executable, not separated into services; packaged applications are also identified by their executable path. System traffic has a System row. Events whose executable cannot be resolved (for example, a protected or already-exited process) have an explicit Unattributed traffic row rather than being silently discarded. Collection failures, detected ETW event loss, and interrupted collection are surfaced in the view; incomplete-history warnings persist across service restarts. Period totals only include times when the service was recording.

## Current development focus

- Targeting .NET 10 for the actively modernised projects.
- Adding the new `ModernTinyWall` desktop client alongside the existing `TinyWall` application.
- Moving expensive UI-triggered operations off the UI thread where possible.
- Improving tray responsiveness and modernising tray command handling.
- Preserving existing TinyWall firewall behaviour while modernising the surrounding application code.
- Prompting users about blocked outbound connections from unapproved applications.
- Keeping native Windows interop explicit and source-generator friendly for modern .NET builds.

You're welcome to hack and slash at it. Enjoy!

#### Hosted on

 - GitHub -> <https://github.com/ShirazAdam/Tinywall>

## How to build

### Necessary tools

- [Microsoft Visual Studio 2026](https://visualstudio.microsoft.com/vs/)
- [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- The [Visual Studio Installer Projects extension](https://marketplace.visualstudio.com/items?itemName=VisualStudioClient.MicrosoftVisualStudio2022InstallerProjects) is needed only to build the legacy `TinyWallSetup` project.
- [Inno Setup 6 or later](https://jrsoftware.org/isinfo.php) is needed to build installers using the current packaging scripts.

### To build the application

1. Open `TinyWall.slnx` in Visual Studio.
1. Build `ModernTinyWall` to run the modern WinUI 3 client, or build `TinyWall` to run the legacy WinForms application.
1. Visual Studio builds the referenced projects automatically.

### To build an installer

The current Inno Setup packaging workflow publishes the `TinyWall` application for x86, x64, and ARM64, then builds an architecture-specific installer. Follow the commands in [`installer/README.md`](installer/README.md). The older `TinyWallSetup` Visual Studio project remains in the solution but is not the primary installer path.

### Main projects

- `ModernTinyWall` - modern .NET 10 Windows desktop client.
- `TinyWall` - original / legacy TinyWall application.
- `TinyWall.Core` - shared core functionality.
- `TinyWall.Shared` - shared source files compiled by both clients.
- `ModernTinyWall.Windows` - Windows platform helpers and native interop wrappers.
- `ModernTinyWall.Windows.WFP` - Windows Filtering Platform integration.
- `ModernTinyWall.Windows.Services` - Windows service-control helpers.
- `ModernTinyWall.Utilities` - shared utility code for the modernised projects.

### To run automated tests

Run the Modern TinyWall exception-store and data-usage tests from the repository root:

```powershell
dotnet test ModernTinyWall.Exceptions.Tests\ModernTinyWall.Exceptions.Tests.csproj
```

The `LiveETW` data-usage smoke test requires an elevated Windows test process; otherwise it is skipped. It records IPv4/IPv6 loopback traffic in a temporary database without modifying the installed service. Release builds store usage in ProgramData; Debug builds follow TinyWall's existing application-directory data location.

### To update/build build the database of known applications

1. Adjust the individual JSON files in the `TinyWall\Database` folder.
1. Start the application with the `/develtool` flag.
1. Use the `Database creator` tab to create one combined database file in JSON format. The output file will be called `profiles.json`.
1. To use the new database in debug builds, copy the output file to the `TinyWall\bin\Debug` folder.
1. Done.

## Contributing

Please feel free to contribute to the project. Fork the repository, make your changes and submit a pull request. All contributions are welcome, whether it's fixing bugs, adding new features, improving documentation, or anything else you think would benefit the project.

## Licence

- All other code and artefacts in the repository are under the DO WHAT THE F*** YOU WANT TO PUBLIC LICENCE. See `LICENCE.txt` for more information.
