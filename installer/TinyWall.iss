#define MyAppName "TinyWall"
#ifndef AppVersion
  #error "AppVersion must be supplied from the matching project versions by build-package.ps1."
#endif
#define MyAppVersion AppVersion
#define MyAppPublisher "Károly Pados"
#define MyAppURL "https://tinywall.pados.hu/"
#ifndef SourceArch
  #error "SourceArch must be supplied by build-package.ps1."
#endif
#ifndef OutputArch
#define OutputArch SourceArch
#endif
#if SourceArch == "win-x86"
  #define AllowedArchitectures "x86 x64"
#elif SourceArch == "win-x64"
  #define AllowedArchitectures "x64"
#elif SourceArch == "win-arm64"
  #define AllowedArchitectures "arm64"
#else
  #error "SourceArch must be win-x86, win-x64, or win-arm64."
#endif
#define PublishDir "..\artifacts\publish\TinyWallDual\" + SourceArch
#if !FileExists(SourcePath + PublishDir + "\catalogue\profiles.db")
  #error "The protected catalogue\profiles.db seed is missing. Run build-package.ps1."
#endif
#if !FileExists(SourcePath + PublishDir + "\package-manifest.json")
  #error "The validated dual-application package manifest is missing. Run build-package.ps1."
#endif

[Setup]
AppId={{1AAB1576-7FE1-4E1B-97EF-752836D76325}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableDirPage=yes
UsePreviousAppDir=no
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer\{#OutputArch}
OutputBaseFilename=TinyWall-{#MyAppVersion}-{#OutputArch}
SetupIconFile=..\TinyWall\Resources\img\firewall.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed={#AllowedArchitectures}
#if SourceArch == "win-x64"
ArchitecturesInstallIn64BitMode=x64
#elif SourceArch == "win-arm64"
ArchitecturesInstallIn64BitMode=arm64
#endif
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
Name: "{group}\Modern TinyWall"; Filename: "{app}\ModernTinyWall.exe"
Name: "{autodesktop}\TinyWall"; Filename: "{app}\TinyWall.exe"; Tasks: desktopicon
Name: "{autodesktop}\Modern TinyWall"; Filename: "{app}\ModernTinyWall.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
Filename: "{app}\TinyWall.exe"; Parameters: "/install"; Description: "Install and start TinyWall service"; Flags: runhidden waituntilterminated; StatusMsg: "Installing TinyWall service..."
Filename: "{app}\ModernTinyWall.exe"; Description: "Launch Modern TinyWall"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\TinyWall.exe"; Parameters: "/uninstall"; Flags: runhidden waituntilterminated; RunOnceId: "TinyWallUninstall"

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if CompareText(RemoveBackslashUnlessRoot(ExpandConstant('{app}')),
    ExpandConstant('{autopf}\TinyWall')) <> 0 then
    Result := 'TinyWall must be installed in its protected Program Files directory. Custom installation directories are not supported.';
end;
