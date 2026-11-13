; HeMi Dark installer (Inno Setup 6), built by release.ps1:  ISCC /DAppVersion=1.1.0 /DSkins=<build\skins> /DOutputDir=<folder>
; For the current user, without administrator rights. It installs every colour and scaling, lets the user pick one and
; (optionally) sets it as the skin of VLC; uninstalling puts VLC back to its usual look if a HeMi Dark skin was in use.
; Silent: HeMiDark-Setup.exe /SILENT [/COLOR=blue] [/SCALE=150] [/NOVLC]

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef Skins
  #define Skins "..\build\skins"
#endif
#ifndef OutputDir
  #define OutputDir "..\build\dist"
#endif

[Setup]
AppId=HeMiDark
AppName=HeMi Dark
AppVersion={#AppVersion}
AppVerName=HeMi Dark Beta {#AppVersion}
AppPublisher=Richard Hemala
AppPublisherURL=https://github.com/Hemal1k/HeMiDark
AppSupportURL=https://github.com/Hemal1k/HeMiDark/issues
AppUpdatesURL=https://github.com/Hemal1k/HeMiDark/releases
DefaultDirName={localappdata}\Programs\HeMiDark
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
PrivilegesRequired=lowest
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=HeMiDark-Setup
UninstallDisplayName=HeMi Dark (VLC skin)
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "cs"; MessagesFile: "compiler:Languages\Czech.isl"

[Files]
Source: "{#Skins}\*"; DestDir: "{app}\skins"; Flags: recursesubdirs ignoreversion
Source: "..\src\Assets\README.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "vlc-skin.ps1"; DestDir: "{app}"; Flags: ignoreversion

[CustomMessages]
en.PageTitle=Colour and size
en.PageDesc=Pick the colour of the skin and the scaling of your screen.
en.Colour=Colour:
en.Scale=Screen scaling (Windows Settings > System > Display > Scale):
en.Now=  (this screen)
en.Apply=Use it in VLC now
en.NoVlc=VLC was not found. The skin is installed anyway; VLC uses it once it is installed.
en.CloseVlc=Please close VLC first – it saves its settings when it closes and would keep the old look.%n%nClose VLC and click OK, or Cancel to set the skin later yourself.
en.DoneApplied=HeMi Dark is installed and set in VLC. Start VLC to see it.%n%nAnother colour or size: run this setup again.
en.DoneManual=HeMi Dark is installed in:%n%1%n%nVLC > Tools > Preferences > Interface > Use custom skin > choose the .vlt > Save > restart VLC.
en.C1=Blue
en.C2=Teal
en.C3=Green
en.C4=Yellow
en.C5=Orange
en.C6=Red
en.C7=Pink
en.C8=Purple
en.C9=Graphite
cs.PageTitle=Barva a velikost
cs.PageDesc=Vyberte barvu vzhledu a měřítko obrazovky.
cs.Colour=Barva:
cs.Scale=Měřítko obrazovky (Nastavení Windows > Systém > Obrazovka > Měřítko):
cs.Now=  (tato obrazovka)
cs.Apply=Hned nastavit ve VLC
cs.NoVlc=VLC nebylo nalezeno. Vzhled se i tak nainstaluje; VLC ho použije, až bude nainstalované.
cs.CloseVlc=Nejdřív prosím zavřete VLC – při zavření ukládá nastavení a nechalo by si starý vzhled.%n%nZavřete VLC a klikněte na OK, nebo na Storno a vzhled nastavíte později sami.
cs.DoneApplied=HeMi Dark je nainstalovaný a nastavený ve VLC. Spusťte VLC.%n%nJiná barva nebo velikost: spusťte tento instalátor znovu.
cs.DoneManual=HeMi Dark je nainstalovaný ve složce:%n%1%n%nVLC > Nástroje > Nastavení > Rozhraní > Použít vlastní vzhled > vyberte .vlt > Uložit > restartujte VLC.
cs.C1=Modrá
cs.C2=Tyrkysová
cs.C3=Zelená
cs.C4=Žlutá
cs.C5=Oranžová
cs.C6=Červená
cs.C7=Růžová
cs.C8=Fialová
cs.C9=Grafitová

[Code]
const
  ColourKeys = 'blue,teal,green,yellow,orange,red,pink,purple,graphite';
  ScaleList = '100,125,140,150,175,200';

var
  Page: TWizardPage;
  ColourBox, ScaleBox: TNewComboBox;
  ApplyBox: TNewCheckBox;
  Applied: Boolean;

function Item(const List: String; Index: Integer): String;
var
  S: String;
  P: Integer;
begin
  S := List + ',';
  while Index > 0 do
  begin
    Delete(S, 1, Pos(',', S));
    Index := Index - 1;
  end;
  P := Pos(',', S);
  Result := Copy(S, 1, P - 1);
end;

function IndexOf(const List, Value: String): Integer;
var
  I: Integer;
begin
  Result := -1;
  for I := 0 to 9 do
    if (Item(List, I) <> '') and (CompareText(Item(List, I), Value) = 0) then
    begin
      Result := I;
      Exit;
    end;
end;

{ the scaling of the screen now: AppliedDPI / 96, the nearest one that has a skin }
function ScreenScale: Integer;
var
  Dpi: Cardinal;
  I, Pct, Best, D: Integer;
begin
  Result := 0;
  if not RegQueryDWordValue(HKCU, 'Control Panel\Desktop\WindowMetrics', 'AppliedDPI', Dpi) then Dpi := 96;
  Pct := Dpi * 100 div 96;
  Best := 1000;
  for I := 0 to 5 do
  begin
    D := StrToInt(Item(ScaleList, I)) - Pct;
    if D < 0 then D := -D;
    if D < Best then
    begin
      Best := D;
      Result := I;
    end;
  end;
end;

function HasParam(const Name: String): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), Name) = 0 then Result := True;
end;

function VlcInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM64, 'SOFTWARE\VideoLAN\VLC') or RegKeyExists(HKLM32, 'SOFTWARE\VideoLAN\VLC')
    or FileExists(ExpandConstant('{commonpf64}\VideoLAN\VLC\vlc.exe')) or FileExists(ExpandConstant('{commonpf32}\VideoLAN\VLC\vlc.exe'));
end;

procedure InitializeWizard;
var
  L: TNewStaticText;
  I, Def, Y: Integer;
  S: String;
begin
  Page := CreateCustomPage(wpLicense, CustomMessage('PageTitle'), CustomMessage('PageDesc'));

  L := TNewStaticText.Create(Page);
  L.Parent := Page.Surface;
  L.Caption := CustomMessage('Colour');
  ColourBox := TNewComboBox.Create(Page);
  ColourBox.Parent := Page.Surface;
  ColourBox.Style := csDropDownList;
  ColourBox.Top := L.Top + L.Height + ScaleY(4);
  ColourBox.Width := ScaleX(220);
  for I := 0 to 8 do ColourBox.Items.Add(CustomMessage('C' + IntToStr(I + 1)));
  Def := IndexOf(ColourKeys, ExpandConstant('{param:COLOR|' + GetPreviousData('Colour', 'blue') + '}'));
  if Def < 0 then Def := 0;
  ColourBox.ItemIndex := Def;
  Y := ColourBox.Top + ColourBox.Height + ScaleY(16);

  L := TNewStaticText.Create(Page);
  L.Parent := Page.Surface;
  L.Top := Y;
  L.Caption := CustomMessage('Scale');
  ScaleBox := TNewComboBox.Create(Page);
  ScaleBox.Parent := Page.Surface;
  ScaleBox.Style := csDropDownList;
  ScaleBox.Top := L.Top + L.Height + ScaleY(4);
  ScaleBox.Width := ScaleX(220);
  for I := 0 to 5 do
  begin
    S := Item(ScaleList, I) + ' %';
    if I = ScreenScale then S := S + CustomMessage('Now');
    ScaleBox.Items.Add(S);
  end;
  Def := IndexOf(ScaleList, ExpandConstant('{param:SCALE|}'));
  if Def < 0 then Def := ScreenScale;
  ScaleBox.ItemIndex := Def;
  Y := ScaleBox.Top + ScaleBox.Height + ScaleY(20);

  ApplyBox := TNewCheckBox.Create(Page);
  ApplyBox.Parent := Page.Surface;
  ApplyBox.Top := Y;
  ApplyBox.Width := Page.SurfaceWidth;
  ApplyBox.Height := ScaleY(20);
  ApplyBox.Caption := CustomMessage('Apply');
  ApplyBox.Checked := not HasParam('/NOVLC');

  if not VlcInstalled then
  begin
    L := TNewStaticText.Create(Page);
    L.Parent := Page.Surface;
    L.Top := ApplyBox.Top + ApplyBox.Height + ScaleY(12);
    L.Width := Page.SurfaceWidth;
    L.WordWrap := True;
    L.Caption := CustomMessage('NoVlc');
  end;
end;

procedure RegisterPreviousData(PreviousDataKey: Integer);
begin
  SetPreviousData(PreviousDataKey, 'Colour', Item(ColourKeys, ColourBox.ItemIndex));
end;

function SkinPath: String;
var
  Key: String;
begin
  Key := Item(ColourKeys, ColourBox.ItemIndex);
  Result := ExpandConstant('{app}\skins\') + Key + '\HeMi-Dark-' + Key + '-' + Item(ScaleList, ScaleBox.ItemIndex) + '.vlt';
end;

function Helper(const Args: String): Integer;
begin
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\vlc-skin.ps1') + '" ' + Args,
    '', SW_HIDE, ewWaitUntilTerminated, Result) then Result := -1;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep <> ssPostInstall) or not ApplyBox.Checked then Exit;
  while Helper('-Running') = 1 do
  begin
    if WizardSilent then Exit;
    if SuppressibleMsgBox(CustomMessage('CloseVlc'), mbInformation, MB_OKCANCEL, IDCANCEL) = IDCANCEL then Exit;
  end;
  Applied := Helper('-Skin "' + SkinPath + '"') = 0;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
    if Applied then
      WizardForm.FinishedLabel.Caption := CustomMessage('DoneApplied')
    else
      WizardForm.FinishedLabel.Caption := FmtMessage(CustomMessage('DoneManual'), [ExtractFileDir(SkinPath)]);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    Helper('-Reset -Folder "' + ExpandConstant('{app}') + '"');
end;
