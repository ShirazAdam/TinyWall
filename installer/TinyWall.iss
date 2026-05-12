#define MyAppName "TinyWall"
#define MyAppVersion "3.5.1"
#define MyAppPublisher "Károly Pados"
#define MyAppURL "https://tinywall.pados.hu/"
#ifndef SourceArch
#define SourceArch "win-x64"
#endif
#ifndef OutputArch
#define OutputArch SourceArch
#endif
#define PublishDir "..\artifacts\publish\TinyWall\" + SourceArch

[Setup]
AppId={{1AAB1576-7FE1-4E1B-97EF-752836D76325}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer\{#OutputArch}
OutputBaseFilename=TinyWall-{#MyAppVersion}-{#OutputArch}
SetupIconFile=..\TinyWall\Resources\img\firewall.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64 arm64
UninstallDisplayIcon={app}\TinyWall.exe
CloseApplications=yes
RestartApplications=no
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TinyWall"; Filename: "{app}\TinyWall.exe"
Name: "{autodesktop}\TinyWall"; Filename: "{app}\TinyWall.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
Filename: "{app}\TinyWall.exe"; Parameters: "/install"; Description: "Install and start TinyWall service"; Flags: runhidden waituntilterminated; StatusMsg: "Installing TinyWall service..."
Filename: "{app}\TinyWall.exe"; Description: "Launch TinyWall"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\TinyWall.exe"; Parameters: "/uninstall"; Flags: runhidden waituntilterminated; RunOnceId: "TinyWallUninstall"
