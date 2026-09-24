; Let me sleep — Windows installer (Inno Setup 6).
; Build:  ISCC.exe /DSourceDir=<unpacked package folder> /DOutputDir=<output folder> distribution\installer\LetMeSleep.iss
; SourceDir is the folder produced by work/package-v030.ps1 (the one next to the ZIP, with BUILD.json).
; Per-user install (no administrator): %LOCALAPPDATA%\Programs\Let me sleep. The game needs nothing else on
; Windows 10/11 x64: DirectX 11 and the Universal CRT ship with Windows, and the Visual C++ runtime DLLs the EOS
; online plugin needs (msvcp140, vcruntime140, vcruntime140_1) are deployed app-local inside the package.

#define AppVersion "0.3.0"
#ifndef SourceDir
  #error Pass /DSourceDir=<unpacked package folder>
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif

[Setup]
AppId={{8C2F4E7A-5B1D-4B8E-9C3A-6F0D2E4A7B91}
AppName=Let me sleep
AppVersion={#AppVersion}
AppVerName=Let me sleep {#AppVersion}
AppPublisher=SaurioGames
AppPublisherURL=https://github.com/Sauri0/LetMeSleep
AppSupportURL=https://github.com/Sauri0/LetMeSleep/issues
AppUpdatesURL=https://github.com/Sauri0/LetMeSleep/releases
DefaultDirName={localappdata}\Programs\Let me sleep
DefaultGroupName=Let me sleep
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=Let-me-sleep-{#AppVersion}-Setup
SetupIconFile=..\..\launcher\assets\launcher.ico
UninstallDisplayIcon={app}\Let-me-sleep.exe
UninstallDisplayName=Let me sleep
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}.0
VersionInfoProductName=Let me sleep
VersionInfoDescription=Instalador de Let me sleep

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el Escritorio"; GroupDescription: "Accesos directos:"

[InstallDelete]
; A previous version's data must never mix with this one (stale or half-written files crash the player at startup).
Type: filesandordirs; Name: "{app}\Let-me-sleep_Data"
Type: filesandordirs; Name: "{app}\MonoBleedingEdge"
Type: filesandordirs; Name: "{app}\D3D12"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Let me sleep"; Filename: "{app}\Let-me-sleep.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Let me sleep"; Filename: "{app}\Let-me-sleep.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Let-me-sleep.exe"; WorkingDir: "{app}"; Description: "Jugar a Let me sleep"; Flags: nowait postinstall skipifsilent
