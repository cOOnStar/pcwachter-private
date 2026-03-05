#define MyAppName "PC Wächter"
#define MyAppExeName "PC Wächter GUI.exe"
#define MyAppPublisher "PC Wächter"
#define MyAppURL "https://github.com/cOOnStar/pcwaechter"

#ifndef MyBrandChannel
  #define MyBrandChannel "prod"
#endif

#define MySetupIcon "..\..\..\shared\assets\branding\" + MyBrandChannel + "\pcwaechter-brand.ico"
#define MyShortcutIconName "pcwaechter-brand.ico"

; Übergabe optional via ISCC /DMyAppVersion=1.0.0 /DPublishDir=...
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\..\apps\desktop\bin\Release\net10.0-windows\win-x64\publish"
#endif

[Setup]
AppId={{A14E7F2B-7E79-4C7E-9A5E-5C117FB2A38C}
AppName={#MyAppName}
AppVerName={#MyAppName} {#MyAppVersion}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppCopyright=© {#MyAppPublisher}
DefaultDirName={autopf}\PCWaechter
DefaultGroupName=PC Wächter
DisableProgramGroupPage=yes
OutputDir=..\..\release\artifacts
OutputBaseFilename=PCWaechter_offline_installer
SetupIconFile={#MySetupIcon}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Installationsprogramm für PC Wächter
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIconCustom}"; GroupDescription: "{cm:AdditionalIconsCustom}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#MySetupIcon}"; DestDir: "{app}"; DestName: "{#MyShortcutIconName}"; Flags: ignoreversion

[Icons]
Name: "{group}\PC Wächter"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyShortcutIconName}"
Name: "{autodesktop}\PC Wächter"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyShortcutIconName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,PC Wächter}"; Flags: nowait postinstall skipifsilent

[CustomMessages]
german.CreateDesktopIconCustom=Desktop-Verknüpfung erstellen
german.AdditionalIconsCustom=Zusätzliche Symbole
english.CreateDesktopIconCustom=Create desktop shortcut
english.AdditionalIconsCustom=Additional shortcuts
