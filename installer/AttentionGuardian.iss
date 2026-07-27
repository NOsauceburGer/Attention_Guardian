#ifndef SourceDir
  #error SourceDir must point to the published application directory.
#endif

#ifndef RuntimeInstaller
  #error RuntimeInstaller must point to WindowsAppRuntimeInstall-x64.exe.
#endif

#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

[Setup]
AppId={{8A28462C-A196-4D6E-95E1-E04512EB4D2C}
AppName=Attention Guardian
AppVersion=0.1.0
AppVerName=Attention Guardian 0.1.0
AppPublisher=Attention Guardian contributors
DefaultDirName={localappdata}\Programs\Attention Guardian
DefaultGroupName=Attention Guardian
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=AttentionGuardian-0.1.0-win-x64-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
SetupIconFile=..\src\AttentionGuardian.Desktop\Assets\Attention_Guardian_icon_high.ico
UninstallDisplayName=Attention Guardian
UninstallDisplayIcon={app}\AttentionGuardian.Desktop.exe
VersionInfoVersion=0.1.0.0
VersionInfoProductName=Attention Guardian
VersionInfoProductVersion=0.1.0
VersionInfoDescription=Attention Guardian Windows installer

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RuntimeInstaller}"; DestDir: "{tmp}"; DestName: "WindowsAppRuntimeInstall-x64.exe"; Flags: deleteafterinstall

[Icons]
Name: "{group}\Attention Guardian"; Filename: "{app}\AttentionGuardian.Desktop.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Attention Guardian"; Filename: "{app}\AttentionGuardian.Desktop.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加快捷方式："; Flags: unchecked

[Run]
Filename: "{tmp}\WindowsAppRuntimeInstall-x64.exe"; Parameters: "--quiet"; StatusMsg: "正在安装 Microsoft Windows App Runtime…"; Flags: runhidden waituntilterminated
Filename: "{app}\AttentionGuardian.Desktop.exe"; Description: "启动 Attention Guardian"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
