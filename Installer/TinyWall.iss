; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "TinyWall"
#define MyAppNameNoVersion "TinyWall"
#define MyAppVersion "1.9.2"
#define MyAppPublisher "K�roly Pados"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppID={{0686F23B-A0DD-45FD-A733-F70A11E8CE64}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={pf}\{#MyAppNameNoVersion}
DefaultGroupName={#MyAppNameNoVersion}
DisableProgramGroupPage=yes
OutputBaseFilename=TinyWallInstaller
Compression=lzma2/Ultra64
SolidCompression=true
ChangesAssociations=false
CreateAppDir=true
InternalCompressLevel=Ultra64
MinVersion=,6.0.6000
Uninstallable=true
AlwaysShowComponentsList=false
ArchitecturesAllowed=x64 x86
ArchitecturesInstallIn64BitMode=x64
TerminalServicesAware=false
AllowUNCPath=false
AppMutex=Global\TinyWallService
UninstallDisplayName=TinyWall
UninstallDisplayIcon={app}\TinyWall.exe
PrivilegesRequired=admin
ShowLanguageDialog=no
AppCopyright=Copyright (c) 2011-2012 {#MyAppPublisher}
AppVerName={#MyAppVersion}
VersionInfoVersion={#MyAppVersion}
AllowCancelDuringInstall=false
LicenseFile=Components/License.txt

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "Components\License.txt"; DestDir: "{app}"
Source: "Components\FAQ.pdf"; DestDir: "{app}"
Source: "Components\Attributions.txt"; DestDir: "{app}"
Source: "Components\TinyWall.exe.config"; DestDir: "{app}"; Flags:
Source: "Components\CommonAppData\*"; DestDir: "{commonappdata}"; Flags: recursesubdirs createallsubdirs 

Source: "Components\TinyWall.exe"; DestDir: "{app}"; Flags: replacesameversion;
Source: "Components\Interop.NetFwTypeLib.dll"; DestDir: "{app}"; Flags: replacesameversion;
Source: "Components\TinyWall.XmlSerializers.dll"; DestDir: "{app}"; Flags: replacesameversion;

[Run]
Filename: "{app}\TinyWall.exe"; Parameters: "/install"; StatusMsg: "Setting up system..."; Flags: runascurrentuser;
Filename: "{app}\TinyWall.exe"; Parameters: "/desktop"; StatusMsg: "Starting user interface..."; Flags: runasoriginaluser nowait; Tasks: not detectnow;
Filename: "{app}\TinyWall.exe"; Parameters: "/desktop /autowhitelist"; StatusMsg: "user interface..."; Flags: runasoriginaluser nowait; Tasks: detectnow;

[Tasks]
Name: detectnow; Description: "After setup, automatically unblock some applications (recommended)"; Flags: checkedonce;

;[UninstallRun]
;Filename: "{app}\TinyWall.exe"; Parameters: "/uninstall"; StatusMsg: "Uninstalling service..."; Flags: runascurrentuser;

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
Type: filesandordirs; Name: "{commonappdata}\TinyWall"

[Icons]
Name: "{group}\TinyWall Controller"; Filename: "{app}\TinyWall.exe"; Parameters: "/desktop"; IconFilename: {app}\TinyWall.exe; Flags: excludefromshowinnewinstall; Comment: "TinyWall Controller"; WorkingDir: "{app}";
Name: "{group}\DevelTool"; Filename: "{app}\TinyWall.exe"; Parameters: "/develtool"; IconFilename: {app}\TinyWall.exe; Flags: excludefromshowinnewinstall; Comment: "TinyWall Development Helper"; WorkingDir: "{app}";
Name: "{group}\FAQ"; Filename: "{app}\FAQ.pdf"; Flags: excludefromshowinnewinstall;
Name: "{group}\Visit Website"; Filename: "http://tinywall.pados.hu"; Flags: excludefromshowinnewinstall;

[CustomMessages]
dotnetmissing=This application needs Microsoft .Net 3.5 which is not yet installed on this computer. Would you like to download it now?
CustomForm_Caption=Important notice
CustomForm_Description=Please read the following carefully to avoid confusion
CustomForm_Label1_Caption0= \
   After installation finishes, TinyWall is configured to block user traffic.%n \
   This means in general you won't be able to access the internet. To grant specific %n \
   applications internet access, use the "Whitelist by..." options from the tray menu.

[Code]
Var
Page: TWizardPage;
Label1: TLabel;
    
function InitializeSetup(): Boolean;
var
    ErrorCode: Integer;
    netFramework35Installed : Boolean;
    netFramework40Installed : Boolean;
    isInstalled: Cardinal;
begin
  result := true;

    // Check for the .Net 3.5 framework
  isInstalled := 0;
  netFramework35Installed := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5', 'Install', isInstalled);
  if ((netFramework35Installed)  and (isInstalled <> 1)) then netFramework35Installed := false;
 
  isInstalled := 0;
  netFramework40Installed := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Client', 'Install', isInstalled);
  if ((netFramework40Installed)  and (isInstalled <> 1)) then netFramework40Installed := false;

  if ((netFramework35Installed = false) and (netFramework40Installed = false)) then
  begin
    if (MsgBox(ExpandConstant('{cm:dotnetmissing}'),
        mbConfirmation, MB_YESNO) = idYes) then
    begin
      ShellExec('open',
      'http://www.microsoft.com/download/en/details.aspx?id=22',
      '','',SW_SHOWNORMAL,ewNoWait,ErrorCode);
    end;
    result := false;
  end;
 
end;

procedure InitializeWizard;
begin
  Page := CreateCustomPage( wpLicense, ExpandConstant('{cm:CustomForm_Caption}'), ExpandConstant('{cm:CustomForm_Description}') );
  { Label1 } Label1 := TLabel.Create(Page);
   with Label1 do
   begin 
    Parent := Page.Surface;
    Caption := ExpandConstant('{cm:CustomForm_Label1_Caption0}');
    Left := ScaleX(16);
    Top := ScaleY(24);
    Width := ScaleX(400);
    Height := ScaleY(400);
    Font.Color := clRed;
   end;
end;

function InitializeUninstall(): Boolean;
var
  ErrorCode: Integer;
begin
  Exec(ExpandConstant('{app}\TinyWall.exe'), '/uninstall', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
  result := (ErrorCode = 0);
end;
