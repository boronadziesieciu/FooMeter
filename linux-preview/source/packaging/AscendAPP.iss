#ifndef AppVersion
#define AppVersion "0.10.29"
#endif
#ifndef AppPublishDir
#define AppPublishDir "..\artifacts\publish"
#endif
#ifndef AppOutputDir
#define AppOutputDir "..\artifacts\dist"
#endif
#ifndef AppIconPath
#define AppIconPath "..\src\AscendAPP.Desktop\Assets\FooMeter.ico"
#endif

[Setup]
AppId={{17BE9E27-5D6C-4927-9E38-71C68AD22F09}
AppName=FooMeter
AppVersion={#AppVersion}
AppVerName=FooMeter {#AppVersion}
AppPublisher=FooMeter
DefaultDirName={localappdata}\Programs\FooMeter
DefaultGroupName=FooMeter
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
DisableProgramGroupPage=yes
Compression=lzma2
SolidCompression=yes
OutputDir={#AppOutputDir}
OutputBaseFilename=FooMeter-Setup-{#AppVersion}-win-x64
SetupIconFile={#AppIconPath}
UninstallDisplayIcon={app}\FooMeter.exe
UninstallDisplayName=FooMeter
ChangesAssociations=no
CloseApplications=force
RestartApplications=no
VersionInfoVersion={#AppVersion}.0
VersionInfoDescription=FooMeter Windows Installer
VersionInfoProductName=FooMeter

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#AppPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\FooMeter"; Filename: "{app}\FooMeter.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\FooMeter"; Filename: "{app}\FooMeter.exe"; Tasks: desktopicon; WorkingDir: "{app}"

[Run]
Filename: "{app}\FooMeter.exe"; Description: "Launch FooMeter"; Flags: nowait postinstall skipifsilent
