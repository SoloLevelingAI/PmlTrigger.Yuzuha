[Setup]
AppId=Core30.Phayton.SkinPreview
AppName=Phayton Setup Style Preview
AppVersion=0.3.4.2
DefaultDirName={localappdata}\YuzuhaToolkit\PmlTrigger.Yuzuha
DefaultGroupName=Phayton Preview
PrivilegesRequired=lowest
Uninstallable=no
CreateUninstallRegKey=no
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
SetupIconFile=assets\yuzuha.ico
LicenseFile=..\LICENSE
ShowLanguageDialog=no
OutputBaseFilename=Phayton-Skin-Preview

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl,ChineseSimplified.isl"

[Messages]
english.WelcomeLabel1=Welcome to Phayton
english.WelcomeLabel2=Choose where to install Phayton and which AVEVA environments to connect.%n%nReview the installation plan before applying the changes.
english.ButtonInstall=End preview
english.ReadyLabel1=The installation plan has been previewed. Go back to change an option or end the preview.
chinesesimp.WelcomeLabel1=欢迎使用 Phayton
chinesesimp.WelcomeLabel2=选择安装位置和需要接入的 AVEVA 环境。%n%n在确认安装前，可核对 EVARS、随包文件和可选 Skill 的变更。
chinesesimp.ButtonInstall=结束预览
chinesesimp.ReadyLabel1=已浏览安装计划。可返回修改选择，或结束预览。

#include "theme\Core30Light.iss"

[Code]
var
  SimpleResult: TWizardPage;

function T(ZH, EN: String): String;
begin
  Result := Core30LabelText(ZH, EN);
end;

function Q(Value: String): String;
begin
  Result := '"' + Value + '"';
end;

procedure ExtractSupport;
begin
end;

function Bridge(Exe, Operation, Arguments: String; ShowMode: Integer): String;
var Path: String;
begin
  Result := '';
  if Operation <> 'discover-native' then Exit;
  Path := ExpandConstant('{tmp}\discovery.ini');
  SetIniInt('discovery', 'count', 2, Path);
  SetIniString('row0', 'product', 'AVEVA E3D 3.1', Path);
  SetIniString('row0', 'path', 'C:\AVEVA\E3D3.1\evars.init', Path);
  SetIniString('row0', 'profile', 'E3D3.1.6', Path);
  SetIniString('row0', 'evidence', T('示例安装路径', 'Sample installation path'), Path);
  SetIniString('row1', 'product', 'AVEVA Marine 12.1', Path);
  SetIniString('row1', 'path', 'C:\AVEVA\Marine12.1\evars.bat', Path);
  SetIniString('row1', 'profile', 'AM', Path);
  SetIniString('row1', 'evidence', T('示例安装路径', 'Sample installation path'), Path);
  SetIniString('row1', 'noInit', '1', Path);
end;

#include "NativePages.iss"

procedure ShowSimpleResult(Sender: TWizardPage);
begin
  WizardForm.NextButton.Caption := T('完成', 'Done');
end;

procedure InitializeWizard;
var PreviewPage: TWizardPage; Preview: TNewMemo; Text: TNewStaticText;
begin
  Core30InitializeLightSkin('Phayton', T('Phayton 安装预览', 'Phayton Setup Preview'), '0.3.4.2',
    T('Phayton 是 PmlTrigger 的桌面管理器。', 'Phayton is the desktop manager for PmlTrigger.'), Core30StandardEntry);
  if Core30Compact then begin
    SimpleResult := CreateCustomPage(wpSelectDir, T('预览完成', 'Preview complete'), '');
    SimpleResult.OnActivate := @ShowSimpleResult;
    Core30StyleCustomPage(SimpleResult);
    Core30Card(SimpleResult.Surface, 0, ScaleY(31), SimpleResult.SurfaceWidth, ScaleY(172), {#Core30Surface}, {#Core30Border});
    Core30Text(SimpleResult.Surface, ScaleX(24), ScaleY(58), SimpleResult.SurfaceWidth - ScaleX(48), ScaleY(49),
      {#Core30TitlePt}, T('预览完成', 'Preview complete'), {#Core30Ink}, True);
    Text := Core30Text(SimpleResult.Surface, ScaleX(24), ScaleY(123), SimpleResult.SurfaceWidth - ScaleX(48), ScaleY(51),
      {#Core30LabelPt}, T('正式安装结束后，可从 PmlTwin 打开 EVARS 配置。',
      'After a real installation, open EVARS settings from PmlTwin.'), {#Core30Muted}, False);
    Text.WordWrap := True;
    Exit;
  end;
  CreateNativePages;
  PreviewPage := CreateCustomPage(AIPage.ID, T('安装计划预览', 'Installation plan preview'),
    T('核对文件和环境配置的变更', 'Review the files and environment settings'));
  Core30StyleCustomPage(ModePage);
  Core30StyleCustomPage(ProductPage);
  Core30StyleCustomPage(AIPage);
  Core30StyleCustomPage(PreviewPage);
  Preview := TNewMemo.Create(PreviewPage);
  Preview.Parent := PreviewPage.Surface;
  Preview.SetBounds(0, 0, PreviewPage.SurfaceWidth, PreviewPage.SurfaceHeight);
  Preview.ReadOnly := True;
  Preview.Font.Name := 'Consolas';
  Preview.Lines.Add(T('预览示例', 'Preview sample'));
  Preview.Lines.Add('');
  Preview.Lines.Add(T('安装路径', 'Install destination') + '  C:\Users\Example\AppData\Local\YuzuhaToolkit');
  Preview.Lines.Add(T('环境配置', 'Environment') + '  C:\AVEVA\E3D3.1\evars.init');
  Preview.Lines.Add(T('变更', 'Changes') + '  + set "Yuzuha=%~dp0PmlTwin"');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  if Core30Compact then begin
    Result := True;
    if CurPageID = wpSelectDir then begin
      Result := Core30AcceptedTerms and (Core30FolderError = '');
      if Result then Result := Core30ConfirmOverwrite('');
      Exit;
    end;
    if CurPageID = SimpleResult.ID then begin
      Result := False;
      Core30CloseWizard(nil);
    end;
    Exit;
  end;
  Result := CurPageID <> wpReady;
  if not Result then WizardForm.Close;
end;

procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  if CurPageID = wpReady then Confirm := False;
  if Core30Compact then
    if CurPageID = SimpleResult.ID then Confirm := False;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := 'The style preview has no installation payload.';
end;
