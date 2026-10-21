; External Access Links Manager setup. Installs for the current user only, with no admin rights, into
; %APPDATA%\Autodesk\ApplicationPlugins, which Revit 2024-2027 all read.
;
; Built by MRD-Tools\Deploy.targets (dotnet build MRDTools.csproj -t:Deploy), which stages the bundle first and
; passes AppVersion and StageDir.
;
; Silent install:   <setup>.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
; Trying a setup without touching Revit's folders:   <setup>.exe /TestRoot=<an empty folder>
;   installs the bundle under <folder>\ApplicationPlugins\ instead; uninstall it afterwards from Apps.

#ifndef AppVersion
  #error Pass /DAppVersion=<the csproj Version>
#endif
#ifndef StageDir
  #error Pass /DStageDir=<the staging folder>
#endif

#define AppName "External Access Links Manager"
#define AppPublisher "Mike Richardson Design"
#define BundleName "External Access Links Manager.bundle"

[Setup]
; A fixed AppId, so a new version upgrades the old one in place
AppId={{2D5C2E04-DB5A-4324-8DFE-5EC513A4CB7D}
PrivilegesRequired=lowest
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://www.mikerichardsondesign.com
AppCopyright=Copyright (c) Mike Richardson
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} setup
; {app} only holds the uninstaller: the add-in itself goes to the ApplicationPlugins folder below
DefaultDirName={autopf}\{#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern
SetupIconFile=MRD-Tools.ico
UninstallDisplayIcon={uninstallexe}
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; Clear the previous version's bundle first, so no files from an older layout are left behind
[InstallDelete]
Type: filesandordirs; Name: "{code:PluginsDir|ApplicationPlugins}\{#BundleName}"

[Files]
Source: "{#StageDir}\ApplicationPlugins\{#BundleName}\*"; DestDir: "{code:PluginsDir|ApplicationPlugins}\{#BundleName}"; Flags: recursesubdirs createallsubdirs ignoreversion

[UninstallDelete]
Type: filesandordirs; Name: "{code:PluginsDir|ApplicationPlugins}\{#BundleName}"

[Code]
// An ApplicationPlugins folder Revit reads: this user's, or one of the two all-users ones (Revit 2024-2026 read
// ProgramData, Revit 2027 reads Program Files). /TestRoot=<folder> puts each under that folder instead, so a setup
// can be tried without touching the folders Revit reads.
function PluginsDir(Location: String): String;
var
  TestRoot: String;
begin
  TestRoot := ExpandConstant('{param:TestRoot|}');
  if TestRoot <> '' then
    Result := AddBackslash(TestRoot) + Location
  else if Location = 'ProgramData' then
    Result := ExpandConstant('{commonappdata}\Autodesk\ApplicationPlugins')
  else if Location = 'ProgramFiles' then
    Result := ExpandConstant('{commonpf64}\Autodesk\ApplicationPlugins')
  else
    Result := ExpandConstant('{userappdata}\Autodesk\ApplicationPlugins');
end;

// A copy installed for all users as well would give Revit two add-ins with the same AddInId, and it refuses one
function InitializeSetup(): Boolean;
begin
  Result := True;
  if DirExists(PluginsDir('ProgramData') + '\{#BundleName}') or DirExists(PluginsDir('ProgramFiles') + '\{#BundleName}') then
  begin
    SuppressibleMsgBox('{#AppName} is already installed for all users on this computer.' + #13#10#13#10 +
      'A second copy just for you would make Revit refuse one of them, so setup will stop.', mbError, MB_OK, IDOK);
    Result := False;
  end;
end;
