![GitHub Actions: Build TinyWall Workflow](https://github.com/ShirazAdam/TinyWall/actions/workflows/Build-TinyWall-NETFramework.yml/badge.svg)

[![Automatic Dependency Submission](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependency-graph/auto-submission/badge.svg)](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependency-graph/auto-submission)

[![CodeQL](https://github.com/ShirazAdam/TinyWall/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/ShirazAdam/TinyWall/actions/workflows/github-code-scanning/codeql)

[![Dependabot Updates](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependabot/dependabot-updates/badge.svg)](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependabot/dependabot-updates)

# TinyWall

A free, lightweight and non-intrusive firewall. The original project suffered from a lack of updates and support. The original project still suffers from performance problems where right-clicking on the tray icon can take anywhere between 2 - 12 seconds to the show the context menu. Some of the forms also freeze due to the events running on the UI thread. This fork aims to experiment and continue the development of TinyWall, keeping it up-to-date and compatible with the latest Windows versions, while also adding new features and improvements.

You're welcome to hack and slash at it. Enjoy!

## How to build

### Necessary tools

- [Microsoft Visual Studio 2026](https://visualstudio.microsoft.com/vs/)
- [.NET Framework 4.8.1](https://dotnet.microsoft.com/en-us/download/dotnet-framework)
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

- See `LICENCE.txt` for more information.
