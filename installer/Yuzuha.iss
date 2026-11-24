; Windows program setup with PML sources. Activate the deployed copies from PmlTwin.
#ifndef PayloadDir
  #error PayloadDir must identify a verified staged payload.
#endif
#ifndef AppVersion
  #define AppVersion "0.3.5.4"
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
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "\PMLLIB\*,\PMLUI\*,\deployment\*,PMLLIB.zip,PMLUI.zip,pml.index"; Flags: ignoreversion overwritereadonly recursesubdirs createallsubdirs; Check: ShouldCopyProgramFile
Source: "{#PayloadDir}\deployment\{#AppVersion}\*"; DestDir: "{app}\deployment\{#AppVersion}"; Flags: ignoreversion overwritereadonly recursesubdirs nocompression; Check: ShouldCopyProgramFile

#ifdef RetainedFileEntries
[UninstallDelete]
#include RetainedFileEntries
#endif

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
type
  TPreparation = (prepIdle, prepReview, prepBackup, prepReady);

var
  PlanPath, PlanHash, ReportFile, ManifestPath, ReviewFile, PreparationFolder: String;
  RetainedProgramFiles: TStringList;
  Prepared, Applied, Extracted, InstallStarted, StopRequested: Boolean;
  FailureCode: Integer;
  Preparation: TPreparation;
  Worker: THandle;
  WorkerTimer: UINT_PTR;
  WorkerCallback: NativeInt;

function SetPreparationTimer(Window: HWND; Id: UINT_PTR; Delay: UINT; Callback: NativeInt): UINT_PTR;
  external 'SetTimer@user32.dll stdcall';
function KillPreparationTimer(Window: HWND; Id: UINT_PTR): BOOL;
  external 'KillTimer@user32.dll stdcall';
function WaitWorker(Process: THandle; Milliseconds: DWORD): DWORD;
  external 'WaitForSingleObject@kernel32.dll stdcall';
function WorkerExitCode(Process: THandle; var Code: DWORD): BOOL;
  external 'GetExitCodeProcess@kernel32.dll stdcall';
function CloseWorker(Process: THandle): BOOL;
  external 'CloseHandle@kernel32.dll stdcall';
function StopWorker(Process: THandle; Code: UINT): BOOL;
  external 'TerminateProcess@kernel32.dll stdcall';
function PostInstallClick(Window: HWND; Message: UINT; WParam: WPARAM; LParam: LPARAM): BOOL;
  external 'PostMessageW@user32.dll stdcall';

function T(ZH, EN: String): String;
begin
  if ActiveLanguage = 'chinesesimp' then Result := ZH else Result := EN;
end;

function Q(Value: String): String;
begin
  Result := '"' + Value + '"';
end;

#ifndef ProgramUpgradePreview
function RetainProgramFile(FileName: String): Boolean;
begin
  Result := False;
  if RetainedProgramFiles = nil then Exit;
  if Copy(FileName, 1, 4) = '\\?\' then Delete(FileName, 1, 4);
  Result := RetainedProgramFiles.IndexOf(FileName) >= 0;
end;

function ShouldCopyProgramFile: Boolean;
begin
  if RetainedProgramFiles = nil then begin Result := True; Exit; end;
  Result := not RetainProgramFile(ExpandConstant(CurrentFilename));
end;

function RetainedFileName: String;
begin
  Result := ExpandConstant('{tmp}\retained-program-files.txt');
end;

function BeginProgramArguments: String;
begin
  Result := Q(PlanPath) + ' ' + Q(PlanHash) + ' ' + Q(ManifestPath) + ' ' + Q(ExpandConstant('{app}'));
#ifdef RetainedFileEntries
  DeleteFile(RetainedFileName);
  Result := Result + ' ' + Q(RetainedFileName);
#endif
end;

function LoadRetainedProgramFiles: String;
var Content: AnsiString;
begin
  Result := '';
#ifdef RetainedFileEntries
  if not LoadStringFromFile(RetainedFileName, Content) then begin
    Result := T('无法读取安装文件清单。', 'Cannot read the installation file list.');
    Exit;
  end;
  if RetainedProgramFiles = nil then RetainedProgramFiles := TStringList.Create;
  RetainedProgramFiles.Text := UTF8Decode(Content);
  RetainedProgramFiles.Sorted := True;
#endif
end;
#endif

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

function BridgeError(ExitCode: DWORD): String;
var Content: AnsiString;
begin
  Result := '';
  if ExitCode = 0 then Exit;
  if LoadStringFromFile(ReportFile, Content) then Result := UTF8Decode(Content)
  else Result := T('安装辅助程序退出，错误码 ', 'The installer helper exited with code ') + IntToStr(ExitCode);
  Log(Result);
end;

procedure ReleasePreparation;
begin
  if WorkerTimer <> 0 then KillPreparationTimer(0, WorkerTimer);
  WorkerTimer := 0;
  if Worker <> 0 then begin
    if WaitWorker(Worker, 0) = 258 then StopWorker(Worker, 1);
    CloseWorker(Worker);
  end;
  Worker := 0;
end;

procedure CancelPreparation(Sender: TObject);
begin
  if Worker = 0 then Exit;
  StopRequested := True;
  Core30Preparation(True, False, T('正在结束准备…', 'Stopping preparation…'));
  StopWorker(Worker, 1);
end;

procedure PollPreparation(Window: HWND; Message: UINT; Id: UINT_PTR; Tick: DWORD);
  forward;

procedure StartPreparation(Step: TPreparation; Operation, Arguments, MessageText: String);
var ErrorCode: DWORD; Error: String;
begin
  Error := '';
  StopRequested := False;
  Preparation := Step;
  Core30Preparation(True, True, MessageText);
  DeleteFile(ReportFile);
  try
    Worker := Core30StartSetupWorker(ExpandConstant('{tmp}\Yuzuha.SetupBridge.exe'),
      Operation + ' ' + Arguments + ' ' + Q('--report=' + ReportFile), ErrorCode);
    if Worker = 0 then
      Error := T('无法启动安装辅助程序。', 'Cannot start the installer helper.') + ' ' + SysErrorMessage(ErrorCode)
    else begin
      if WorkerCallback = 0 then WorkerCallback := CreateCallback(@PollPreparation);
      WorkerTimer := SetPreparationTimer(0, 0, 80, WorkerCallback);
      if WorkerTimer = 0 then Error := T('无法检查准备进度。', 'Cannot monitor preparation.');
    end;
  except
    Error := GetExceptionMessage;
  end;
  if Error <> '' then begin
    ReleasePreparation;
    Preparation := prepIdle;
    Core30Preparation(False, False, '');
    Core30ShowSetupError(Error);
  end;
end;

function ConfirmPreparedFiles: Boolean;
var Content: AnsiString; Lines: TStringList; Existing: Boolean;
begin
  Result := False;
  Lines := TStringList.Create;
  try
    if LoadStringFromFile(ReviewFile, Content) then Lines.Text := UTF8Decode(Content);
    if Lines.Count < 2 then begin
      Core30ShowSetupError(T('无法读取安装预览。', 'Cannot read the installation preview.'));
      Exit;
    end;
    Existing := Lines[1] = 'upgrade';
    Lines.Delete(1);
    Result := Core30ConfirmOverwrite(Lines.Text, Existing);
  finally
    Lines.Free;
  end;
end;

procedure PollPreparation(Window: HWND; Message: UINT; Id: UINT_PTR; Tick: DWORD);
var Step: TPreparation; WaitResult, Code: DWORD; Error: String;
begin
  if (Worker = 0) or (WorkerTimer <> Id) then Exit;
  WaitResult := WaitWorker(Worker, 0);
  if WaitResult = 258 then Exit;
  Step := Preparation;
  Preparation := prepIdle;
  Code := 1;
  if WaitResult = 0 then WorkerExitCode(Worker, Code);
  ReleasePreparation;
  if StopRequested then begin
    Core30Preparation(False, False, T('已取消准备。', 'Preparation cancelled.'));
    Exit;
  end;
  Error := BridgeError(Code);
  Core30Preparation(False, False, '');
  if Error <> '' then begin
    Core30ShowSetupError(Error);
    Exit;
  end;
  if Step = prepBackup then begin
#ifndef ProgramUpgradePreview
    Error := LoadRetainedProgramFiles;
    if Error <> '' then begin
      Core30ShowSetupError(Error);
      Exit;
    end;
#endif
    Prepared := True;
    Preparation := prepReady;
    PostInstallClick(WizardForm.NextButton.Handle, $00F5, 0, 0);
    Exit;
  end;
  if ExpandConstant('{param:PLAN|}') = '' then PlanHash := GetSHA256OfFile(PlanPath);
  if not ConfirmPreparedFiles then Exit;
#ifdef ProgramUpgradePreview
  MsgBox(T('程序升级预检已通过。此预览只读。', 'The program upgrade passed preflight. This preview is read-only.'), mbInformation, MB_OK);
#else
  StartPreparation(prepBackup, 'begin-program-plan',
    BeginProgramArguments,
    T('正在检查访问权限并备份待覆盖文件。', 'Checking write access and backing up files.'));
#endif
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
  if not WizardSilent then Core30PrepareCancel.OnClick := @CancelPreparation;
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
  ExtractSupport;
  Result := Bridge(ExpandConstant('{tmp}\Yuzuha.SetupBridge.exe'),'begin-program-plan',
    BeginProgramArguments, SW_HIDE);
  if Result = '' then Result := LoadRetainedProgramFiles;
  Prepared := Result = '';
end;
#endif

function NextButtonClick(CurPageID: Integer): Boolean;
var Request, RequestFile: String;
begin
  Result := True;
  if CurPageID <> wpSelectDir then Exit;
  if WizardSilent then begin
    ExtractSupport;
    if PlanPath = '' then begin
      SuppressibleMsgBox(T('静默安装必须提供批准计划及 SHA256；先按 INSTALL.md 生成预览。',
        'Silent installation requires an approved plan and SHA256. Generate a preview using INSTALL.md.'),mbError,MB_OK,IDOK);
      Result := False;
    end;
    Exit;
  end;
  Result := False;
  if Preparation = prepReady then begin
    Preparation := prepIdle;
    if (ExpandConstant('{app}') = PreparationFolder) and Core30AcceptedTerms then begin Result := True; Exit; end;
    Prepared := False;
  end;
  if (Worker <> 0) or not Core30AcceptedTerms or (Core30FolderError <> '') then Exit;
  ExtractSupport;
  PreparationFolder := ExpandConstant('{app}');
  DeleteFile(ReviewFile);
  if ExpandConstant('{param:PLAN|}') = '' then begin
    PlanPath := ExpandConstant('{tmp}\approved-plan.json');
    DeleteFile(PlanPath);
    Request := '{"integrationMode":"none","root":'+JsonString(PreparationFolder)+
      ',"mcpJson":"","skillTarget":"","environments":[]}';
    RequestFile := ExpandConstant('{tmp}\native-request.json');
    if not SaveStringToFile(RequestFile,UTF8Encode(Request),False) then begin
      Core30ShowSetupError(T('无法写入安装请求。', 'Cannot write installation request.'));
      Exit;
    end;
    StartPreparation(prepReview, 'plan',
      Q(RequestFile) + ' ' + Q(ManifestPath) + ' ' + Q(ExpandConstant('{tmp}\skill')) + ' ' + Q(PlanPath) +
      ' ' + Q(ReviewFile), T('正在检查安装目录和待更新文件。', 'Checking the folder and files to update.'));
  end else
    StartPreparation(prepReview, 'verify-program-plan',
      Q(PlanPath) + ' ' + Q(PlanHash) + ' ' + Q(ManifestPath) + ' ' + Q(PreparationFolder) + ' ' + Q(ReviewFile),
      T('正在检查安装计划。', 'Checking the installation plan.'));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
#ifdef ProgramUpgradePreview
  Result := 'Read-only program upgrade preview.';
#else
  if Prepared then Result := ''
  else if WizardSilent then Result := BeginProgramInstall
  else Result := T('请返回安装目录页重新准备。', 'Return to the directory page to prepare again.');
#endif
end;

#ifndef ProgramUpgradePreview
function ApplyProgram: String;
var Exe, Arguments: String; Process: THandle; Code: DWORD;
begin
  Exe := ExpandConstant('{tmp}\Yuzuha.SetupBridge.exe');
  Arguments := Q(PlanPath) + ' ' + Q(PlanHash) + ' ' + Q(ManifestPath);
  if WizardSilent then begin
    Result := Bridge(Exe, 'apply-plan', Arguments, SW_HIDE);
    Exit;
  end;
  WizardForm.StatusLabel.Caption := T('正在校验安装文件并保存安装状态。', 'Verifying installed files and saving setup state.');
  WizardForm.FilenameLabel.Caption := '';
  DeleteFile(ReportFile);
  Process := Core30StartSetupWorker(Exe, 'apply-plan ' + Arguments + ' ' + Q('--report=' + ReportFile), Code);
  if Process = 0 then begin
    Result := T('无法启动安装辅助程序。', 'Cannot start the installer helper.') + ' ' + SysErrorMessage(Code);
    Exit;
  end;
  try
    if Core30WaitSetupWorker(Process, Code) then Result := BridgeError(Code)
    else Result := T('无法读取安装校验结果。', 'Cannot read the installation verification result.');
  finally
    CloseWorker(Process);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Error: String;
begin
  if CurStep = ssInstall then InstallStarted := True;
  if CurStep = ssPostInstall then begin
    Error := ApplyProgram;
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

procedure DeinitializeSetup;
var Error: String;
begin
  ReleasePreparation;
  if RetainedProgramFiles <> nil then RetainedProgramFiles.Free;
  if Prepared and InstallStarted and not Applied then begin
    Error := Bridge(ExpandConstant('{tmp}\Yuzuha.SetupBridge.exe'),'abort-plan',Q(PlanPath),SW_HIDE);
    if Error <> '' then Log(Error);
  end;
end;
