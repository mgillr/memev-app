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

[Files]
Source: "{#Src}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{userprograms}\Memev"; Filename: "{app}\memev-desktop.exe"

[Run]
Filename: "{app}\memev-desktop.exe"; Description: "Open Memev"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; the core started from this install is stopped before its files go; the notes folder is the person's and is kept
Filename: "{app}\resources\bin\memev.cmd"; Parameters: "stop"; Flags: runhidden waituntilterminated; RunOnceId: "StopCore"
