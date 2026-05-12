# 04-tinywall-application: TinyWall Application

Upgrade the `TinyWall` WinForms application to .NET 10 for Windows desktop. This is the main application workload and contains the majority of compatibility findings, especially Windows Forms, GDI+ / System.Drawing, legacy configuration, configuration installation components, WMI, Code Access Security, Windows ACLs, and legacy controls.

The task includes updating Microsoft.Extensions packages to their .NET 10 versions, removing packages whose functionality is included by the target framework, preserving Windows desktop behaviour, and ensuring application settings and runtime assumptions still work under modern .NET.

**Done when**: `TinyWall` targets `net10.0-windows`, its package references are compatible with .NET 10, mandatory API issues are resolved, relevant behavioural changes are reviewed, and the application builds successfully.
