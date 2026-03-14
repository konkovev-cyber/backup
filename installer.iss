[Setup]
AppName=BackupSystem Professional
AppVersion=1.0.0
DefaultDirName={pf}\BackupSystem
DefaultGroupName=BackupSystem
OutputBaseFilename=BackupSystem_Setup
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Files]
; UI Files
Source: "publish\UI\*"; DestDir: "{app}\UI"; Flags: recursesubdirs
; Service Files
Source: "publish\Service\*"; DestDir: "{app}\Service"; Flags: recursesubdirs

[Icons]
Name: "{group}\BackupSystem Professional"; Filename: "{app}\UI\BackupSystem.UI.exe"
Name: "{commondesktop}\BackupSystem Professional"; Filename: "{app}\UI\BackupSystem.UI.exe"

[Run]
; Install and Start Service
Filename: "sc.exe"; Parameters: "create BackupSystem binPath= ""{app}\Service\BackupSystem.Service.exe"" start= auto"; Flags: runhidden
Filename: "sc.exe"; Parameters: "description BackupSystem ""Профессиональная система резервного копирования данных"""; Flags: runhidden
Filename: "sc.exe"; Parameters: "start BackupSystem"; Flags: runhidden

[UninstallRun]
; Stop and Delete Service
Filename: "sc.exe"; Parameters: "stop BackupSystem"; Flags: runhidden
Filename: "sc.exe"; Parameters: "delete BackupSystem"; Flags: runhidden

[Code]
// Функция для проверки запущенных процессов и их остановки перед установкой
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  Exec('taskkill', '/F /IM BackupSystem.UI.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill', '/F /IM BackupSystem.Service.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;
