; Windows setup: explicit plan -> preview -> approval -> execution.
#ifndef PayloadDir
  #error PayloadDir must identify a verified staged payload.
#endif
#ifndef AppVersion
  #define AppVersion "0.3.4.2"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{6F551950-F3A9-4D2F-B771-145415993095}
AppName=Phayton
AppVersion={#AppVersion}
AppVerName=Phayton {#AppVersion} — Windows Setup
AppPublisher=SoloLevelingAI
SetupIconFile=assets\yuzuha.ico
UninstallDisplayIcon={app}\setup\yuzuha.ico
AppPublisherURL=https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha
DefaultDirName={localappdata}\YuzuhaToolkit\PmlTrigger.Yuzuha
DefaultGroupName=Phayton
UsePreviousGroup=no
; Request UAC before the wizard starts, not after a plan has been approved.
; Do not allow a command-line downgrade to a non-administrative installation.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern dynamic
WizardSizePercent=120,115
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=Phayton-{#AppVersion}-Windows-Setup
Compression=lzma2
SolidCompression=yes
SetupLogging=yes
CloseApplications=no
RestartApplications=no
UninstallDisplayName=Phayton — Windows Setup {#AppVersion}
UninstallFilesDir={app}\uninstall
UsePreviousAppDir=yes
UsePreviousTasks=no
ShowLanguageDialog=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl,ChineseSimplified.isl"

[Files]
Source: "{#PayloadDir}\setup\Yuzuha.SetupBridge.exe"; Flags: dontcopy
Source: "{#PayloadDir}\setup\Newtonsoft.Json.dll"; Flags: dontcopy
Source: "{#PayloadDir}\.setup-payload.json"; Flags: dontcopy
Source: "{#PayloadDir}\skill\*"; DestDir: "{tmp}\skill"; Flags: dontcopy recursesubdirs createallsubdirs
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Phayton Workspace"; Filename: "{app}\runtime\desktop\YuzuhaToolkit.Desktop.exe"; IconFilename: "{app}\setup\yuzuha.ico"; Check: not WizardNoIcons and FileExists(ExpandConstant('{app}\runtime\desktop\YuzuhaToolkit.Desktop.exe'))
Name: "{group}\Phayton — INSTALL"; Filename: "{app}\INSTALL.md"; IconFilename: "{app}\setup\yuzuha.ico"; Check: not WizardNoIcons
Name: "{group}\Phayton — Folder"; Filename: "{app}"; IconFilename: "{app}\setup\yuzuha.ico"; Check: not WizardNoIcons
Name: "{group}\Uninstall Phayton"; Filename: "{uninstallexe}"; Check: not WizardNoIcons

[UninstallDelete]
Type: files; Name: "{app}\.yuzuha-inno.json"

[Run]
Filename: "{app}\runtime\desktop\YuzuhaToolkit.Desktop.exe"; Parameters: "--prepare-installation --handoff ""{app}"""; Description: "{cm:OpenManagerPreparation}"; Flags: nowait postinstall skipifsilent runasoriginaluser; Check: CanOpenManager

[CustomMessages]
english.OpenManagerPreparation=Open Manager to prepare environments and deployment
chinesesimp.OpenManagerPreparation=打开管理器，准备环境与部署

[Messages]
english.WelcomeLabel1=Welcome to Phayton
english.WelcomeLabel2=CONNECT / UNDERSTAND / RECORD%n%nInstall the application, then continue environment preparation and deployment in Manager.%n%nReview every proposed file change before installation. AVEVA integration remains a separate task.
english.FinishedHeadingLabel=Your Phayton installation is ready
english.FinishedLabel=The approved application plan has been executed.%n%nReports and backups: LocalAppData\YuzuhaToolkit\SetupReports.%n%nContinue in Manager to inspect installation ownership, preview EVARS and deployment, and explicitly apply changes. Application installation does not prove AVEVA is connected.
chinesesimp.WelcomeLabel1=欢迎使用 Phayton
chinesesimp.WelcomeLabel2=连接 / 理解 / 记录%n%n先安装应用，再由管理器准备环境与部署。%n%n检查安装计划中的每一项文件变化，确认后执行；AVEVA 接入是后续独立任务。
chinesesimp.FinishedHeadingLabel=Phayton 已准备就绪
chinesesimp.FinishedLabel=已执行批准的应用安装计划。%n%n实际结果与备份保存在 LocalAppData\YuzuhaToolkit\SetupReports。%n%n继续在管理器核对安装所有权、预览 EVARS 与部署并明确应用；应用安装完成不表示 AVEVA 已接入。

[Code]
var
  PlanPath, PlanHash, ReportFile, ManifestPath: String;
  Prepared, Applied, Extracted: Boolean;
  FailureCode: Integer;
  SummaryPage: TWizardPage;
  Summary: TNewMemo;

function T(ZH, EN: String): String;
begin
  if ActiveLanguage = 'chinesesimp' then Result := ZH else Result := EN;
end;

function Q(Value: String): String;
begin
  Result := '"' + Value + '"';
end;

function CanOpenManager: Boolean;
begin
  Result := Applied and FileExists(ExpandConstant('{app}\runtime\desktop\YuzuhaToolkit.Desktop.exe'));
end;

procedure ExtractSupport;
begin
  if Extracted then Exit;
  ExtractTemporaryFile('Yuzuha.SetupBridge.exe');
  ExtractTemporaryFile('Newtonsoft.Json.dll');
  ExtractTemporaryFile('.setup-payload.json');
  ExtractTemporaryFiles('{tmp}\skill\*');
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

#include "NativePages.iss"

procedure InitializeWizard;
begin
  WizardForm.SelectDirLabel.Caption := T(
    '请选择安装目录。若使用其他管理员账户提权，请核对用户目录及后续 Skill 目标。',
    'Choose the installation directory. If elevated as another administrator, review user folders and the Skill target.');
  ReportFile := ExpandConstant('{tmp}\yuzuha-error.txt');
  ManifestPath := ExpandConstant('{tmp}\.setup-payload.json');
  PlanPath := ExpandConstant('{param:PLAN|}');
  PlanHash := ExpandConstant('{param:PLANHASH|}');
  CreateNativePages;
  SummaryPage := CreateCustomPage(AIPage.ID, T('安装计划预览','Installation plan preview'),
    T('查看 CLI 文件、Skill 和环境配置的具体变化','Review CLI payload, Skill and environment changes'));
  Summary := TNewMemo.Create(SummaryPage);
  Summary.Parent := SummaryPage.Surface;
  Summary.SetBounds(0,0,SummaryPage.SurfaceWidth,SummaryPage.SurfaceHeight);
  Summary.ReadOnly := True;
  Summary.ScrollBars := ssBoth;
  Summary.WordWrap := False;
  Summary.Font.Name := 'Consolas';
  Summary.Font.Size := 9;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var Error, Rendered, Request, RequestFile: String; Content: AnsiString;
begin
  Result := True;
  if ExpandConstant('{param:PLAN|}') <> '' then begin
    if CurPageID <> wpSelectDir then Exit;
  end else begin
    if WizardSilent and (CurPageID = wpSelectDir) then begin
      SuppressibleMsgBox('Silent installation requires /PLAN and /PLANHASH.',mbError,MB_OK,IDOK);
      Result := False; Exit;
    end;
    if CurPageID <> AIPage.ID then Exit;
  end;
  ExtractSupport;
  if ExpandConstant('{param:PLAN|}') = '' then begin
    if WizardSilent then begin
      SuppressibleMsgBox(T('静默安装必须提供批准计划及 SHA256；先按 INSTALL.md 生成预览。',
        'Silent installation requires an approved plan and SHA256. Generate a preview using INSTALL.md.'),mbError,MB_OK,IDOK);
      Result := False; Exit;
    end;
    PlanPath := ExpandConstant('{tmp}\approved-plan.json');
    DeleteFile(PlanPath);
    Request := NativeRequest;
    if Request = '' then begin Result := False; Exit; end;
    RequestFile := ExpandConstant('{tmp}\native-request.json');
    if not SaveStringToFile(RequestFile,UTF8Encode(Request),False) then begin
      MsgBox('Cannot write installation request.',mbError,MB_OK); Result := False; Exit;
    end;
    Error := Bridge(ExpandConstant('{tmp}\Yuzuha.SetupBridge.exe'),'plan',
      Q(RequestFile) + ' ' + Q(ManifestPath) + ' ' + Q(ExpandConstant('{tmp}\skill')) + ' ' + Q(PlanPath), SW_HIDE);
    if Error <> '' then begin SuppressibleMsgBox(Error,mbError,MB_OK,IDOK); Result := False; Exit; end;
    PlanHash := GetSHA256OfFile(PlanPath);
  end;
  if (not FileExists(PlanPath)) or (CompareText(GetSHA256OfFile(PlanPath),PlanHash) <> 0) then begin
    SuppressibleMsgBox(T('计划不存在或 SHA256 不匹配。','Plan missing or SHA256 mismatch.'),mbError,MB_OK,IDOK);
    Result := False; Exit;
  end;
  Rendered := ExpandConstant('{tmp}\preview.md');
  Error := Bridge(ExpandConstant('{tmp}\Yuzuha.SetupBridge.exe'),'render',
    Q(PlanPath) + ' ' + Q(ExpandConstant('{app}')) + ' ' + Q(Rendered), SW_HIDE);
  if Error <> '' then begin SuppressibleMsgBox(Error,mbError,MB_OK,IDOK); Result := False; Exit; end;
  if LoadStringFromFile(Rendered,Content) then Summary.Text := UTF8Decode(Content);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := Bridge(ExpandConstant('{tmp}\Yuzuha.SetupBridge.exe'),'begin-plan',
    Q(PlanPath) + ' ' + Q(PlanHash) + ' ' + Q(ManifestPath), SW_HIDE);
  Prepared := Result = '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Error: String;
begin
  if CurStep = ssPostInstall then begin
    Error := Bridge(ExpandConstant('{tmp}\Yuzuha.SetupBridge.exe'),'apply-plan',
      Q(PlanPath) + ' ' + Q(PlanHash) + ' ' + Q(ManifestPath), SW_HIDE);
    if Error <> '' then begin
      FailureCode := 10;
      WizardForm.FinishedHeadingLabel.Caption := T('安装未完成','Installation incomplete');
      WizardForm.FinishedLabel.Caption := Error;
      RaiseException(Error);
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
  Error := Bridge(ExpandConstant('{app}\setup\Yuzuha.SetupBridge.exe'),'check-remove',Q(ExpandConstant('{app}')),SW_HIDE);
  Result := Error = '';
  if not Result then SuppressibleMsgBox(Error,mbError,MB_OK,IDOK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Error: String;
begin
  if CurUninstallStep = usUninstall then begin
    Error := Bridge(ExpandConstant('{app}\setup\Yuzuha.SetupBridge.exe'),'remove-plan',Q(ExpandConstant('{app}')),SW_HIDE);
    if Error <> '' then RaiseException(Error);
  end;
end;
