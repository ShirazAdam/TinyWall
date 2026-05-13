![GitHub Actions: Build TinyWall Workflow](https://github.com/ShirazAdam/TinyWall/actions/workflows/Build-TinyWall-NETFramework.yml/badge.svg) [![Automatic Dependency Submission](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependency-graph/auto-submission/badge.svg)](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependency-graph/auto-submission) [![CodeQL](https://github.com/ShirazAdam/TinyWall/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/ShirazAdam/TinyWall/actions/workflows/github-code-scanning/codeql) [![Dependabot Updates](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependabot/dependabot-updates/badge.svg)](https://github.com/ShirazAdam/TinyWall/actions/workflows/dependabot/dependabot-updates)

# TinyWall

A free, lightweight and non-intrusive firewall.

### .NET 10 version of TinyWall: https://github.com/ShirazAdam/TinyWall/tree/upgrade-to-NET10

#### Original author's website: https://tinywall.pados.hu

## About this repository

This is forked from the source code of TinyWall as found at its [original author's website](https://tinywall.pados.hu). Upstream development is now largely inactive at the author's site, but this repository is being maintained by me and updated with my ideas or improvements.

#### Hosted on:
 - GitHub -> https://github.com/ShirazAdam/Tinywall
 - CodeBerg (**Archived due to lack of Windows runners**) -> https://codeberg.org/ShirazAdam/Tinywall
 - GitLab (**Archived due to lack of Windows runners**) -> https://gitlab.com/ShirazAdam/TinyWall
 

## How to build

### Necessary tools
- [Microsoft Visual Studio 2022](https://visualstudio.microsoft.com/vs/)
- [.NET Framework 4.8.1](https://dotnet.microsoft.com/en-us/download/dotnet-framework)
- [Microsoft Visual Studio 2022 Installer Project Extension](https://marketplace.visualstudio.com/items?itemName=VisualStudioClient.MicrosoftVisualStudio2022InstallerProjects)

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

Please don't open issues for feature requests or bug reports. Any changes you'd like you will need to implement yourself. If you have improvements that you would like to integrate into TinyWall, please fork the repo and create a pull request.

1. Fork the Project
1. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
1. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
1. Push to the Branch (`git push origin feature/AmazingFeature`)
1. Open a Pull Request

For complex features or large changes, please contact me first if your changes are still within the scope of the application.

If you prefer that, you can also build and distribute your own version of the binaries. In this case though you need to choose a different name other than TinyWall for your application.


## Licence

- Task Dialogue wrapper (code in directory `pylorak.Windows\TaskDialog`) written by KevinGre ([link](https://www.codeproject.com/Articles/17026/TaskDialog-for-WinForms)) and placed under Public Domain.

- All other code in the repository is under the GNU GPLv3 Licence. See `LICENCE.txt` for more information.


## Original Author Contact Details

Károly Pados - find e-mail at the bottom of the project website

Website: [https://tinywall.pados.hu](https://tinywall.pados.hu)

GitHub: [https://github.com/pylorak/tinywall](https://github.com/pylorak/tinywall)
