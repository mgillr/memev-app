; SPDX-License-Identifier: Apache-2.0
; The Windows installer: Memev for this user only, no administrator rights, with an uninstaller. Unsigned; nothing is published.
; build.ps1 passes AppVersion, Src (the staged app) and Out (the output folder).
[Setup]
AppId=computer.memev.Desktop
AppName=Memev
AppVersion={#AppVersion}
AppVerName=Memev {#AppVersion}
DefaultDirName={localappdata}\Programs\Memev
DisableDirPage=auto
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#Out}
OutputBaseFilename=Memev-{#AppVersion}-windows-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\memev-desktop.exe
UninstallDisplayName=Memev
; an update replaces files a running Memev holds open: close the window and its core first (Restart Manager, then the stop below)
CloseApplications=force
CloseApplicationsFilter=*.exe,*.dll
RestartApplications=no

[Files]
Source: "{#Src}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{userprograms}\Memev"; Filename: "{app}\memev-desktop.exe"

[Run]
Filename: "{app}\memev-desktop.exe"; Description: "Open Memev"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; the core started from this install is stopped before its files go; the notes folder is the person's and is kept
Filename: "{app}\resources\bin\memev.cmd"; Parameters: "stop"; Flags: runhidden waituntilterminated; RunOnceId: "StopCore"

[Code]
// Before the files are copied, stop the core an earlier install started, so its node.exe and git do not hold files open.
// A first install has no memev.cmd yet, and a stop that fails does not stop the install: Restart Manager closes the rest.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Cmd: String;
  Code: Integer;
begin
  Result := '';
  Cmd := ExpandConstant('{app}\resources\bin\memev.cmd');
  if FileExists(Cmd) then
    Exec(ExpandConstant('{cmd}'), '/c ""' + Cmd + '" stop"', '', SW_HIDE, ewWaitUntilTerminated, Code);
end;
