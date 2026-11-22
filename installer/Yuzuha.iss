; Windows program setup with PML sources. Activate the deployed copies from PmlTwin.
#ifndef PayloadDir
  #error PayloadDir must identify a verified staged payload.
#endif
#ifndef AppVersion
  #define AppVersion "0.3.5.3"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

[Setup]
#ifdef ProgramUpgradePreview
AppId=Core30.Phayton.ProgramUpgradePreview
AppName=Phayton Upgrade Preview
PrivilegesRequired=lowest
CreateUninstallRegKey=no
Uninstallable=no
OutputBaseFilename=Phayton-{#AppVersion}-Upgrade-Preview
#else
AppId={{6F551950-F3A9-4D2F-B771-145415993095}
AppName=Phayton
PrivilegesRequired=admin
UninstallLogMode=new
OutputBaseFilename=Phayton-{#AppVersion}-Windows-Setup
#endif
AppVersion={#AppVersion}
AppVerName=Phayton {#AppVersion} — Windows Setup
AppComments={cm:PhaytonDesktopManager}
AppPublisher=SoloLevelingAI
SetupIconFile=assets\yuzuha.ico
UninstallDisplayIcon={app}\setup\yuzuha.ico
AppPublisherURL=https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha
DefaultDirName={localappdata}\YuzuhaToolkit\PmlTrigger.Yuzuha
DefaultGroupName=Phayton
UsePreviousGroup=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir={#OutputDir}
Compression=lzma2
SolidCompression=yes
SetupLogging=yes
CloseApplications=no
RestartApplications=no
UninstallDisplayName=Phayton — Windows Setup {#AppVersion}
UninstallFilesDir={app}\uninstall
UsePreviousAppDir=yes
UsePreviousTasks=no
ShowLanguageDialog=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl,ChineseSimplified.isl"

[CustomMessages]
english.PhaytonDesktopManager=Phayton is the desktop manager for PmlTrigger.
chinesesimp.PhaytonDesktopManager=Phayton 是 PmlTrigger 的桌面管理器。
english.OpenWorkspace=Open Phayton Workspace
chinesesimp.OpenWorkspace=打开 Phayton Workspace 管理器

[Files]
Source: "{#PayloadDir}\setup\Yuzuha.SetupBridge.exe"; Flags: dontcopy
Source: "{#PayloadDir}\setup\Newtonsoft.Json.dll"; Flags: dontcopy
Source: "{#PayloadDir}\.setup-payload.json"; Flags: dontcopy
#ifndef ProgramUpgradePreview
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "\PMLLIB\*,\PMLUI\*,\deployment\*,PMLLIB.zip,PMLUI.zip,pml.index"; Flags: ignoreversion overwritereadonly recursesubdirs createallsubdirs
Source: "{#PayloadDir}\deployment\{#AppVersion}\*"; DestDir: "{app}\deployment\{#AppVersion}"; Flags: ignoreversion overwritereadonly recursesubdirs nocompression

[Icons]
Name: "{group}\Phayton Workspace"; Filename: "{app}\runtime\desktop\PmlTwin.Workbench.exe"; WorkingDir: "{app}\runtime\desktop"; IconFilename: "{app}\setup\yuzuha.ico"; Check: not WizardNoIcons
Name: "{group}\Phayton — INSTALL"; Filename: "{app}\INSTALL.md"; IconFilename: "{app}\setup\yuzuha.ico"; Check: not WizardNoIcons
Name: "{group}\Phayton — Folder"; Filename: "{app}"; IconFilename: "{app}\setup\yuzuha.ico"; Check: not WizardNoIcons
Name: "{group}\Uninstall Phayton"; Filename: "{uninstallexe}"; Check: not WizardNoIcons

[Run]
Filename: "{app}\runtime\desktop\PmlTwin.Workbench.exe"; WorkingDir: "{app}\runtime\desktop"; Description: "{cm:OpenWorkspace}"; Flags: postinstall nowait runasoriginaluser skipifsilent; Check: CanOpenWorkspace

#endif

[Messages]
english.FinishedHeadingLabel=Your Phayton installation is ready
english.FinishedLabel=Open File Deployment in PmlTwin to deploy shared PML sources and configure the detected AVEVA installations.%n%nReports and backups: LocalAppData\YuzuhaToolkit\SetupReports.
chinesesimp.FinishedHeadingLabel=Phayton 已准备就绪
chinesesimp.FinishedLabel=在 PmlTwin 的文件部署中部署共用 PML 源码，并预览各 AVEVA 版本的 EVARS 接入。%n%n报告与备份位于 LocalAppData\YuzuhaToolkit\SetupReports。

#include "theme\Core30Light.iss"

[Code]
var
  PlanPath, PlanHash, ReportFile, ManifestPath, ReviewFile: String;
  Prepared, Applied, Extracted: Boolean;
  FailureCode: Integer;

function T(ZH, EN: String): String;
begin
  if ActiveLanguage = 'chinesesimp' then Result := ZH else Result := EN;
end;

function Q(Value: String): String;
begin
  Result := '"' + Value + '"';
end;

function JsonString(Value: String): String;
var I: Integer; C: String;
begin
  Result := '"';
  for I := 1 to Length(Value) do begin
    C := Copy(Value, I, 1);
    if C = '\' then Result := Result + '\\'
    else if C = '"' then Result := Result + '\"'
    else if Ord(Value[I]) < 32 then Result := Result + '\u00' +
      Copy('0123456789abcdef', (Ord(Value[I]) div 16) + 1, 1) + Copy('0123456789abcdef', (Ord(Value[I]) mod 16) + 1, 1)
    else Result := Result + C;
  end;
  Result := Result + '"';
end;

procedure ExtractSupport;
begin
  if Extracted then Exit;
  ExtractTemporaryFile('Yuzuha.SetupBridge.exe');
  ExtractTemporaryFile('Newtonsoft.Json.dll');
  ExtractTemporaryFile('.setup-payload.json');
  Extracted := True;
end;

function Bridge(Exe, Operation, Arguments: String; ShowMode: Integer): String;
var ExitCode: Integer; Content: AnsiString;
begin
  Result := '';
  DeleteFile(ReportFile);
  if not Exec(Exe, Operation + ' ' + Arguments + ' ' + Q('--report=' + ReportFile),
    '', ShowMode, ewWaitUntilTerminated, ExitCode) then
    Result := T('无法启动辅助程序，需要 .NET Framework 4.8。','Cannot launch helper; .NET Framework 4.8 is required.')
  else if ExitCode <> 0 then begin
    if LoadStringFromFile(ReportFile, Content) then Result := UTF8Decode(Content)
    else Result := T('安装辅助程序失败。','Installer helper failed.') + ' ' + IntToStr(ExitCode);
  end;
  if Result <> '' then Log(Result);
end;

procedure InitializeWizard;
begin
#ifdef ProgramUpgradePreview
  Core30InitializeLightSkin('Phayton', T('Phayton 只读升级预检', 'Phayton Read-only Upgrade Preview'), '{#AppVersion}',
    ExpandConstant('{cm:PhaytonDesktopManager}'), True);
#else
  Core30InitializeLightSkin('Phayton', T('Phayton 安装程序', 'Phayton Setup'), '{#AppVersion}',
    ExpandConstant('{cm:PhaytonDesktopManager}'), True);
#endif
  ReportFile := ExpandConstant('{tmp}\yuzuha-error.txt');
  ManifestPath := ExpandConstant('{tmp}\.setup-payload.json');
  ReviewFile := ExpandConstant('{tmp}\program-replacements.txt');
  PlanPath := ExpandConstant('{param:PLAN|}');
  PlanHash := ExpandConstant('{param:PLANHASH|}');
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = wpWelcome) or (PageID = wpLicense) or (PageID = wpReady);
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  Core30LightPageChanged(CurPageID);
  if (CurPageID = wpFinished) and (FailureCode <> 0) then begin
    WizardForm.FinishedHeadingLabel.Caption := T('安装未完成', 'Installation incomplete');
    WizardForm.FinishedHeadingLabel.Font.Color := {#Core30Danger};
    WizardForm.FinishedLabel.Caption := T('请查看错误提示和本次安装日志。', 'See the error message and setup log.');
  end;
end;

#ifndef ProgramUpgradePreview
function CanOpenWorkspace: Boolean;
begin
  Result := Applied;
end;

function BeginProgramInstall: String;
begin
  Result := Bridge(ExpandConstant('{tmp}\Yuzuha.SetupBridge.exe'),'begin-program-plan',
    Q(PlanPath) + ' ' + Q(PlanHash) + ' ' + Q(ManifestPath) + ' ' + Q(ExpandConstant('{app}')), SW_HIDE);
  Prepared := Result = '';
end;
#endif

function NextButtonClick(CurPageID: Integer): Boolean;
var Error, Destination, Request, RequestFile: String;
    Review: AnsiString;
begin
  Result := True;
  if CurPageID <> wpSelectDir then Exit;
  if not WizardSilent and (not Core30AcceptedTerms or (Core30FolderError <> '')) then begin Result := False; Exit; end;
  ExtractSupport;
  Destination := ExpandConstant('{app}');
  if ExpandConstant('{param:PLAN|}') = '' then begin
    if WizardSilent then begin
      SuppressibleMsgBox(T('静默安装必须提供批准计划及 SHA256；先按 INSTALL.md 生成预览。',
        'Silent installation requires an approved plan and SHA256. Generate a preview using INSTALL.md.'),mbError,MB_OK,IDOK);
      Result := False; Exit;
    end;
    PlanPath := ExpandConstant('{tmp}\approved-plan.json');
    DeleteFile(PlanPath);
    Request := '{"integrationMode":"none","root":'+JsonString(Destination)+
      ',"mcpJson":"","skillTarget":"","environments":[]}';
    RequestFile := ExpandConstant('{tmp}\native-request.json');
    if not SaveStringToFile(RequestFile,UTF8Encode(Request),False) then begin
      MsgBox('Cannot write installation request.',mbError,MB_OK); Result := False; Exit;
    end;
    Error := Bridge(ExpandConstant('{tmp}\Yuzuha.SetupBridge.exe'),'plan',
      Q(RequestFile) + ' ' + Q(ManifestPath) + ' ' + Q(ExpandConstant('{tmp}\skill')) + ' ' + Q(PlanPath) +
      ' ' + Q(ReviewFile), SW_HIDE);
    if Error <> '' then begin Core30ShowSetupError(Error); Result := False; Exit; end;
    PlanHash := GetSHA256OfFile(PlanPath);
  end;
  if (not FileExists(PlanPath)) or (CompareText(GetSHA256OfFile(PlanPath),PlanHash) <> 0) then begin
    SuppressibleMsgBox(T('计划不存在或 SHA256 不匹配。','Plan missing or SHA256 mismatch.'),mbError,MB_OK,IDOK);
    Result := False; Exit;
  end;
  if not WizardSilent and (ExpandConstant('{param:PLAN|}') <> '') then begin
    Error := Bridge(ExpandConstant('{tmp}\Yuzuha.SetupBridge.exe'),'verify-program-plan',
      Q(PlanPath) + ' ' + Q(PlanHash) + ' ' + Q(ManifestPath) + ' ' + Q(Destination) + ' ' + Q(ReviewFile), SW_HIDE);
    if Error <> '' then begin Core30ShowSetupError(Error); Result := False; Exit; end;
  end;
  if not WizardSilent then begin
    if not LoadStringFromFile(ReviewFile, Review) then begin
      MsgBox(T('无法读取安装预览。', 'Cannot read the installation preview.'), mbError, MB_OK);
      Result := False; Exit;
    end;
    Result := Core30ConfirmOverwrite(UTF8Decode(Review));
    if not Result then Exit;
  end;
#ifdef ProgramUpgradePreview
  if Result then MsgBox(T('程序升级预检已通过。此预览只读。','The program upgrade passed preflight. This preview is read-only.'), mbInformation, MB_OK);
  Result := False;
#else
  if not WizardSilent then begin
    Error := BeginProgramInstall;
    if Error <> '' then begin Core30ShowSetupError(Error); Result := False; end;
  end;
#endif
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
#ifdef ProgramUpgradePreview
  Result := 'Read-only program upgrade preview.';
#else
  if Prepared then Result := '' else Result := BeginProgramInstall;
#endif
end;

#ifndef ProgramUpgradePreview
procedure CurStepChanged(CurStep: TSetupStep);
var Error: String;
begin
  if CurStep = ssPostInstall then begin
    Error := Bridge(ExpandConstant('{tmp}\Yuzuha.SetupBridge.exe'),'apply-plan',
      Q(PlanPath) + ' ' + Q(PlanHash) + ' ' + Q(ManifestPath), SW_HIDE);
    if Error <> '' then begin
      FailureCode := 10;
      if not WizardSilent then Core30ShowSetupError(Error);
      Exit;
    end;
    Applied := True;
  end;
end;

function GetCustomSetupExitCode: Integer;
begin
  Result := FailureCode;
end;

procedure DeinitializeSetup;
var Error: String;
begin
  if Prepared and not Applied then begin
    Error := Bridge(ExpandConstant('{tmp}\Yuzuha.SetupBridge.exe'),'abort-plan',Q(PlanPath),SW_HIDE);
    if Error <> '' then Log(Error);
  end;
end;

function InitializeUninstall: Boolean;
var Error: String;
begin
  ReportFile := ExpandConstant('{tmp}\yuzuha-uninstall-error.txt');
  Error := Bridge(ExpandConstant('{app}\setup\Yuzuha.SetupBridge.exe'),'check-program-remove',Q(ExpandConstant('{app}')),SW_HIDE);
  Result := Error = '';
  if not Result then SuppressibleMsgBox(Error,mbError,MB_OK,IDOK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Error: String;
begin
  if CurUninstallStep = usUninstall then begin
    Error := Bridge(ExpandConstant('{app}\setup\Yuzuha.SetupBridge.exe'),'remove-program-registration',Q(ExpandConstant('{app}')),SW_HIDE);
    if Error <> '' then RaiseException(Error);
  end;
end;
#endif
