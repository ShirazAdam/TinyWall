[![.NET 10 Desktop](https://github.com/ShirazAdam/TinyWall/actions/workflows/dotnet-desktop.yml/badge.svg?branch=upgrade-to-NET10)](https://github.com/ShirazAdam/TinyWall/actions/workflows/dotnet-desktop.yml)

[![Automatic Dependency Submission](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependency-graph/auto-submission/badge.svg)](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependency-graph/auto-submission)

[![CodeQL](https://github.com/ShirazAdam/TinyWall/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/ShirazAdam/TinyWall/actions/workflows/github-code-scanning/codeql)

[![Dependabot Updates](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependabot/dependabot-updates/badge.svg)](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependabot/dependabot-updates)

# TinyWall

TinyWall is a free, lightweight and non-intrusive firewall for Windows. It is designed to harden the built-in Windows Firewall while keeping day-to-day use simple: no noisy pop-ups, no bundled drivers, and no unnecessary background clutter.

The application provides a tray-based control centre for managing firewall modes, application exceptions, service and process rules, connection activity, blocklists, and firewall log entries. It helps users allow trusted applications, block unwanted network access, and review network activity without needing to edit Windows Firewall rules manually.

TinyWall focuses on being practical and low-overhead. It uses Windows networking and security features rather than replacing them, and aims to keep the user interface responsive while doing heavier discovery and rule-management work in the background.

You're welcome to hack and slash at it. Enjoy!

#### Hosted on

 - GitHub -> <https://github.com/ShirazAdam/Tinywall>

## How to build

### Necessary tools

- [Microsoft Visual Studio 2026](https://visualstudio.microsoft.com/vs/)
- [.NET 10](https://dotnet.microsoft.com/en-us/download/dotnet-framework)
- [Microsoft Visual Studio 2022/2026 Installer Project Extension](https://marketplace.visualstudio.com/items?itemName=VisualStudioClient.MicrosoftVisualStudio2022InstallerProjects)

### To build the application

1. Open the solution file in Visual Studio and compile the `TinyWall` project. The other projects referenced inside the solution need not be compiled separately as they will be statically compiled into the application.
1. Done.

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

- All other code in the repository is under the GNU GPLv3 Licence. See `LICENCE.txt` for more information.