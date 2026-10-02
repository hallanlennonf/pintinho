; Instalador do Pintinho (Inno Setup 6). Gerado pelo release.bat:
;   ISCC /DAppVersion=x.y.z installer\Pintinho.iss
; Instala por usuário (sem pedir administrador), então a atualização automática roda em silêncio.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{8C1E0E1A-4B4E-4B7B-9A2E-5A1D2C3B4F60}
AppName=Pintinho
AppVersion={#AppVersion}
AppVerName=Pintinho {#AppVersion}
AppPublisher=Hallan
AppPublisherURL=https://github.com/hallanlennonf/pintinho
DefaultDirName={localappdata}\Programs\Pintinho
DefaultGroupName=Pintinho
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyPage=yes
PrivilegesRequired=lowest
OutputDir=..\release
OutputBaseFilename=Pintinho-Setup-{#AppVersion}
SetupIconFile=..\assets\pintinho.ico
UninstallDisplayIcon={app}\Pintinho.exe
UninstallDisplayName=Pintinho
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"
Name: "startup"; Description: "Abrir o Pintinho quando o Windows iniciar"; Flags: unchecked

[Dirs]
Name: "{app}\desenhos"
Name: "{app}\desenhos-aconchego"

[Files]
Source: "..\dist\Pintinho.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\desenhos\LEIA-ME.txt"; DestDir: "{app}\desenhos"; Flags: ignoreversion
Source: "..\dist\desenhos-aconchego\LEIA-ME.txt"; DestDir: "{app}\desenhos-aconchego"; Flags: ignoreversion

[Icons]
Name: "{group}\Pintinho"; Filename: "{app}\Pintinho.exe"
Name: "{userdesktop}\Pintinho"; Filename: "{app}\Pintinho.exe"; Tasks: desktopicon
Name: "{userstartup}\Pintinho"; Filename: "{app}\Pintinho.exe"; Tasks: startup

[Run]
; instalação normal: caixinha "Abrir o Pintinho" no final
Filename: "{app}\Pintinho.exe"; Description: "Abrir o Pintinho agora"; Flags: nowait postinstall skipifsilent
; atualização silenciosa (botão Atualizar do app): reabre sozinho
Filename: "{app}\Pintinho.exe"; Flags: nowait skipifnotsilent

[Code]
// Espera o app fechar (ele mesmo fecha ao iniciar a atualização).
function InitializeSetup(): Boolean;
var
  i: Integer;
begin
  i := 0;
  while CheckForMutexes('PintinhoAppMutex') and (i < 50) do
  begin
    Sleep(200);
    i := i + 1;
  end;
  Result := True;
end;
