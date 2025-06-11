[Setup]
AppName=Pixel Motion Photo Player
AppVersion=1.0.0
AppPublisher=Pixel MP Player
AppPublisherURL=https://github.com/user/pixel-mp-player
AppSupportURL=https://github.com/user/pixel-mp-player/issues
AppUpdatesURL=https://github.com/user/pixel-mp-player/releases
DefaultDirName={autopf}\PixelMpPlayer
DefaultGroupName=Pixel Motion Photo Player
AllowNoIcons=yes
LicenseFile=..\LICENSE.txt
InfoBeforeFile=..\README.md
OutputDir=output
OutputBaseFilename=PixelMpPlayer-Setup-v1.0.0
SetupIconFile=..\icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 0,6.1
Name: "associate"; Description: "Motion Photo ファイル (.jpg, .jpeg) を関連付ける"; GroupDescription: "ファイル関連付け:"; Flags: unchecked

[Files]
Source: "..\bin\Release\net8.0-windows\win-x64\publish\PixelMpPlayer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion; DestName: "LICENSE.txt"

[Icons]
Name: "{group}\Pixel Motion Photo Player"; Filename: "{app}\PixelMpPlayer.exe"
Name: "{group}\{cm:ProgramOnTheWeb,Pixel Motion Photo Player}"; Filename: "https://github.com/user/pixel-mp-player"
Name: "{group}\{cm:UninstallProgram,Pixel Motion Photo Player}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Pixel Motion Photo Player"; Filename: "{app}\PixelMpPlayer.exe"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\Pixel Motion Photo Player"; Filename: "{app}\PixelMpPlayer.exe"; Tasks: quicklaunchicon

[Registry]
Root: HKCR; Subkey: ".jpg\OpenWithProgids"; ValueType: string; ValueName: "PixelMpPlayer.MotionPhoto"; ValueData: ""; Tasks: associate
Root: HKCR; Subkey: ".jpeg\OpenWithProgids"; ValueType: string; ValueName: "PixelMpPlayer.MotionPhoto"; ValueData: ""; Tasks: associate
Root: HKCR; Subkey: "PixelMpPlayer.MotionPhoto"; ValueType: string; ValueName: ""; ValueData: "Motion Photo"; Tasks: associate
Root: HKCR; Subkey: "PixelMpPlayer.MotionPhoto\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\PixelMpPlayer.exe,0"; Tasks: associate
Root: HKCR; Subkey: "PixelMpPlayer.MotionPhoto\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\PixelMpPlayer.exe"" ""%1"""; Tasks: associate

[Run]
Filename: "{app}\PixelMpPlayer.exe"; Description: "{cm:LaunchProgram,Pixel Motion Photo Player}"; Flags: nowait postinstall skipifsilent