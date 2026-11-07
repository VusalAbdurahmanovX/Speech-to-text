#ifndef AppVersion
  #define AppVersion "0.1.4"
#endif
#ifndef PublishDir
  #define PublishDir "..\dist\package-0.1.4-win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

[Setup]
AppId={{C9AC8443-2C53-43FA-8618-21B5E65FF436}
AppName=Speech to Text
AppVersion={#AppVersion}
AppPublisher=Vusal Abdurahmanov
DefaultDirName={localappdata}\Programs\SubtitleStudio
DefaultGroupName=Speech to Text
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
OutputDir={#OutputDir}
OutputBaseFilename=Speech-to-Text-Setup-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
UninstallDisplayIcon={app}\SubtitleStudio.exe
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Speech to Text"; Filename: "{app}\SubtitleStudio.exe"
Name: "{autodesktop}\Speech to Text"; Filename: "{app}\SubtitleStudio.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SubtitleStudio.exe"; Description: "Launch Speech to Text"; Flags: nowait postinstall skipifsilent
