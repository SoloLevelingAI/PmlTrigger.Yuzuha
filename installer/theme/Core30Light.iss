#include "Core30Light.Tokens.iss"

#ifndef Core30SkinAssetDir
  #define Core30SkinAssetDir "theme"
#endif

[Setup]
WizardStyle=modern light windows11 hidebevels
WizardSizePercent=150,138
WizardBackColor={#Core30CanvasHex}
WizardImageFile=
WizardSmallImageFile=
DirExistsWarning=no

[Files]
Source: "{#Core30SkinAssetDir}\Core30Frame.dll"; Flags: dontcopy solidbreak
Source: "{#Core30SkinAssetDir}\core30-setup-art.png"; Flags: dontcopy
Source: "..\LICENSE"; DestName: "core30-apache-license.txt"; Flags: dontcopy

[Code]
#include "Core30Frame.iss"

var
  Core30Compact, Core30LicenseLoaded: Boolean;
  Core30TitleClose: TNewButton;
  Core30Terms: TNewCheckBox;
  Core30TermsLink: TNewButton;
  Core30PrepareCancel: TNewButton;
  Core30FolderHint: TNewStaticText;
  Core30Preparing: Boolean;
  Core30LicenseForm, Core30OverwriteForm: TSetupForm;
  Core30InstallButtonWidth, Core30TitleHeight, Core30PadX, Core30PaneW, Core30PaneH: Integer;

function Core30LabelText(ZH, EN: String): String;
begin
  if ActiveLanguage = 'chinesesimp' then Result := ZH else Result := EN;
end;

procedure Core30TextStyle(LabelControl: TNewStaticText; Points: Integer; Ink: TColor; Strong: Boolean);
begin
  LabelControl.StyleElements := [seClient, seBorder];
  if ActiveLanguage = 'chinesesimp' then LabelControl.Font.Name := '{#Core30ChineseFont}';
  LabelControl.Font.Size := Points;
  LabelControl.Font.Color := Ink;
  if Strong then LabelControl.Font.Style := [fsBold] else LabelControl.Font.Style := [];
end;

function Core30Text(Host: TWinControl; X, Y, W, H, Points: Integer;
  Caption: String; Ink: TColor; Strong: Boolean): TNewStaticText;
begin
  Result := TNewStaticText.Create(Host);
  Result.Parent := Host;
  Result.AutoSize := False;
  Result.SetBounds(X, Y, W, H);
  Result.Caption := Caption;
  Core30TextStyle(Result, Points, Ink, Strong);
end;

function Core30Image(Host: TWinControl; X, Y, W, H: Integer): TBitmapImage;
begin
  Result := TBitmapImage.Create(Host);
  Result.Parent := Host;
  Result.AutoSize := False;
  Result.SetBounds(X, Y, W, H);
end;

procedure Core30Fill(Image: TBitmapImage; Base, Face, Stroke: TColor; Radius: Integer);
var W, H, Diameter: Integer;
begin
  W := Image.Width;
  H := Image.Height;
  Image.Bitmap.Width := W;
  Image.Bitmap.Height := H;
  Image.Bitmap.Canvas.Brush.Color := Base;
  Image.Bitmap.Canvas.Pen.Color := Base;
  Image.Bitmap.Canvas.Rectangle(0, 0, W, H);
  Image.Bitmap.Canvas.Brush.Color := Face;
  Image.Bitmap.Canvas.Pen.Color := Stroke;
  Diameter := ScaleX(2 * Radius);
  if Radius = 0 then Image.Bitmap.Canvas.Rectangle(0, 0, W, H)
  else Image.Bitmap.Canvas.RoundRect(0, 0, W, H, Diameter, Diameter);
  Image.Invalidate;
end;

procedure Core30Card(Host: TWinControl; X, Y, W, H: Integer; Face, Edge: TColor);
var Image: TBitmapImage;
begin
  Image := Core30Image(Host, X, Y, W, H);
  Core30Fill(Image, {#Core30Canvas}, Face, Edge, {#Core30CardRadius});
  Image.SendToBack;
end;

procedure Core30PageBackground(Page: TNewNotebookPage; Color: TColor);
begin
  Page.StyleElements := [];
  Page.ParentBackground := False;
  Page.Color := Color;
end;

function Core30NativeButton(Host: TWinControl; Caption: String; X, Y, W, H, Role: Integer;
  Clicked: TNotifyEvent): TNewButton;
begin
  Result := TNewButton.Create(Host);
  Result.Parent := Host;
  Result.Caption := Caption;
  Result.SetBounds(X, Y, W, H);
  Result.OnClick := Clicked;
  Result.Font.Size := {#Core30LabelPt};
  if Role = Core30ButtonPrimary then Result.Font.Style := [fsBold];
  if (Role = Core30ButtonMinimize) or (Role = Core30ButtonClose) then begin
    Result.Font.Size := 17;
    Result.Hint := Caption;
    Result.ShowHint := True;
  end;
  Core30StyleButton(Result, Role);
end;

procedure Core30CloseWizard(Sender: TObject);
var Left: Integer;
begin
  if WizardForm.CurPageID = wpFinished then begin
    Core30SendMessage(WizardForm.NextButton.Handle, $00F5, 0, 0);
    Exit;
  end;
  if not (Core30Compact and not WizardForm.CancelButton.Visible) then begin
    WizardForm.Close;
    Exit;
  end;
  if not WizardForm.CancelButton.Enabled then Exit;
  // Inno's cancellation checks whether its Cancel button can receive focus.
  Left := WizardForm.CancelButton.Left;
  WizardForm.CancelButton.Left := -WizardForm.CancelButton.Width - 1;
  WizardForm.CancelButton.Visible := True;
  try
    WizardForm.Close;
  finally
    WizardForm.CancelButton.Visible := False;
    WizardForm.CancelButton.Left := Left;
  end;
end;

procedure Core30Escape(Sender: TObject; var Key: Word; Shift: TShiftState);
begin
  if Key = 27 then begin
    Key := 0;
    Core30CloseWizard(Sender);
  end;
end;

procedure Core30MinimizeWizard(Sender: TObject);
begin
  Core30ShowWindow(WizardForm.Handle, 6);
end;

procedure Core30CloseTerms(Sender: TObject);
begin
  Core30LicenseForm.Close;
end;

procedure Core30Title(Form: TSetupForm; Title: String; CloseAction: TNotifyEvent; CanMinimize: Boolean);
var Badge: TBitmapImage; Mark, TitleLabel: TNewStaticText; CloseButton: TNewButton; Right, Width: Integer;
begin
  Width := ScaleX(40);
  Right := Form.ClientWidth - ScaleX(12);
  Badge := Core30Image(Form, ScaleX(20), ScaleY(12), ScaleX(25), ScaleY(25));
  Core30Fill(Badge, {#Core30Canvas}, {#Core30Primary}, {#Core30Primary}, 7);
  Mark := Core30Text(Form, Badge.Left, Badge.Top + ScaleY(3), Badge.Width, Badge.Height,
    12, 'P', {#Core30OnPrimary}, True);
  Mark.Alignment := taCenter;
  TitleLabel := Core30Text(Form, ScaleX(55), ScaleY(14), Form.ClientWidth - ScaleX(180), ScaleY(25),
    {#Core30LabelPt}, Title, {#Core30Ink}, True);
  if Core30FrameAvailable then begin
    Core30BindCaption(Mark.Handle);
    Core30BindCaption(TitleLabel.Handle);
  end;
  CloseButton := Core30NativeButton(Form, Core30LabelText('关闭', 'Close'), Right - Width, ScaleY(7), Width,
    ScaleY(34), Core30ButtonClose, CloseAction);
  if CanMinimize then begin
    Core30TitleClose := CloseButton;
    Core30NativeButton(Form, Core30LabelText('最小化', 'Minimize'), Right - 2 * Width - ScaleX(4), ScaleY(7),
      Width, ScaleY(34), Core30ButtonMinimize, @Core30MinimizeWizard);
  end;
end;

procedure Core30TermsDocument(Sender: TObject);
var Memo: TNewMemo; Body: AnsiString; CloseButton: TNewButton; Pad: Integer;
begin
  if not Core30LicenseLoaded then begin
    ExtractTemporaryFile('core30-apache-license.txt');
    Core30LicenseLoaded := True;
  end;
  if not LoadStringFromFile(ExpandConstant('{tmp}\core30-apache-license.txt'), Body) then begin
    MsgBox(Core30LabelText('无法读取协议正文。','The license text could not be read.'), mbError, MB_OK);
    Exit;
  end;
  if Core30LicenseForm = nil then begin
    Core30LicenseForm := CreateCustomForm(600, 438, False, False);
    Core30LicenseForm.Caption := 'Apache License 2.0';
    Core30LicenseForm.PopupMode := pmExplicit;
    Core30LicenseForm.PopupParent := WizardForm;
    Core30LicenseForm.Position := poOwnerFormCenter;
    Core30AttachFrame(Core30LicenseForm, Core30TitleHeight);
    Core30Title(Core30LicenseForm, 'Apache License 2.0', @Core30CloseTerms, False);
    Pad := ScaleX({#Core30CardInset});
    Memo := TNewMemo.Create(Core30LicenseForm);
    Memo.Parent := Core30LicenseForm;
    Memo.SetBounds(Pad, Core30TitleHeight, Core30LicenseForm.ClientWidth - 2 * Pad,
      Core30LicenseForm.ClientHeight - Core30TitleHeight - ScaleY(74));
    Memo.ReadOnly := True;
    Memo.ScrollBars := ssVertical;
    Memo.WordWrap := True;
    Memo.Text := UTF8Decode(Body);
    CloseButton := Core30NativeButton(Core30LicenseForm, Core30LabelText('返回', 'Done'),
      Core30LicenseForm.ClientWidth - Pad - ScaleX(106), Core30LicenseForm.ClientHeight - ScaleY(58),
      ScaleX(106), ScaleY(38), Core30ButtonPrimary, @Core30CloseTerms);
    CloseButton.Cancel := True;
    Core30LicenseForm.ActiveControl := Memo;
  end;
  Core30LicenseForm.ShowModal;
end;

function Core30AcceptedTerms: Boolean;
begin
  Result := (Core30Terms <> nil) and Core30Terms.Checked;
end;

function Core30HasInstallRecord(Folder: String): Boolean;
begin
  Result := FileExists(AddBackslash(Folder) + '.yuzuha-inno.json');
end;

procedure Core30CloseNotice(Sender: TObject);
begin
  TSetupForm(TNewButton(Sender).Parent).Close;
end;

procedure Core30ShowSetupError(MessageText: String);
var Notice: TSetupForm; MessageBox: TNewMemo; Done: TNewButton; Pad: Integer;
begin
  Notice := CreateCustomForm(576, 292, False, False);
  try
    Notice.Caption := Core30LabelText('安装遇到问题', 'Setup issue');
    Notice.PopupMode := pmExplicit;
    Notice.PopupParent := WizardForm;
    Notice.Position := poOwnerFormCenter;
    Core30AttachFrame(Notice, Core30TitleHeight);
    Notice.ClientWidth := ScaleX(576);
    Notice.ClientHeight := ScaleY(292);
    Core30Title(Notice, Notice.Caption, @Core30CloseNotice, False);
    Pad := ScaleX({#Core30CardInset});
    MessageBox := TNewMemo.Create(Notice);
    MessageBox.Parent := Notice;
    MessageBox.SetBounds(Pad, ScaleY(59), Notice.ClientWidth - 2 * Pad, ScaleY(166));
    MessageBox.Font.Size := {#Core30LabelPt};
    MessageBox.WordWrap := True;
    MessageBox.ReadOnly := True;
    MessageBox.ScrollBars := ssVertical;
    MessageBox.Text := MessageText;
    Done := Core30NativeButton(Notice, Core30LabelText('确定', 'OK'),
      Notice.ClientWidth - Pad - ScaleX(112), ScaleY(240), ScaleX(112), ScaleY(38), Core30ButtonPrimary, nil);
    Done.Default := True;
    Done.Cancel := True;
    Done.ModalResult := mrOk;
    Notice.ActiveControl := Done;
    Notice.ShowModal;
  finally
    Notice.Free;
  end;
end;

procedure Core30CloseOverwrite(Sender: TObject);
begin
  Core30OverwriteForm.Close;
end;

function Core30ConfirmOverwrite(Review: String; Existing: Boolean): Boolean;
var Folder: String; Pad, Width, ButtonsY: Integer; BackButton, Proceed: TNewButton;
    Body: TNewStaticText; PathField: TNewEdit; Files: TNewMemo; Lines: TStringList;
begin
  Result := True;
  Folder := Trim(WizardForm.DirEdit.Text);
  if not Existing then Exit;
  Core30OverwriteForm := CreateCustomForm(582, 372, False, False);
  Lines := TStringList.Create;
  try
    Lines.Text := Review;
    Core30OverwriteForm.Caption := Core30LabelText('覆盖安装', 'Replace installation');
    Core30OverwriteForm.PopupMode := pmExplicit;
    Core30OverwriteForm.PopupParent := WizardForm;
    Core30OverwriteForm.Position := poOwnerFormCenter;
    Core30AttachFrame(Core30OverwriteForm, Core30TitleHeight);
    Core30OverwriteForm.ClientWidth := ScaleX(558);
    if Lines.Count > 1 then Core30OverwriteForm.ClientHeight := ScaleY(366)
    else Core30OverwriteForm.ClientHeight := ScaleY(258);
    Core30Title(Core30OverwriteForm, Core30OverwriteForm.Caption, @Core30CloseOverwrite, False);
    Pad := ScaleX({#Core30CardInset});
    Width := Core30OverwriteForm.ClientWidth;
    Core30Text(Core30OverwriteForm, Pad, ScaleY(55), Width - 2 * Pad, ScaleY(30),
      {#Core30SectionPt}, Core30LabelText('覆盖现有程序文件？', 'Replace existing program files?'), {#Core30Ink}, True);
    Body := Core30Text(Core30OverwriteForm, Pad, ScaleY(91), Width - 2 * Pad, ScaleY(35),
      {#Core30LabelPt}, Core30LabelText('同名程序文件将先备份再覆盖。', 'Setup will back up existing program files before replacing them.'), {#Core30Muted}, False);
    Body.WordWrap := True;
    PathField := TNewEdit.Create(Core30OverwriteForm);
    PathField.Parent := Core30OverwriteForm;
    PathField.Font.Size := 10;
    PathField.SetBounds(Pad, ScaleY(128), Width - 2 * Pad, ScaleY(29));
    PathField.Text := Folder;
    PathField.ReadOnly := True;
    if Lines.Count > 0 then begin
      Core30Text(Core30OverwriteForm, Pad, ScaleY(165), Width - 2 * Pad, ScaleY(20),
        {#Core30LabelPt}, Core30LabelText('备份目录', 'Backup directory'), {#Core30Muted}, False);
      PathField := TNewEdit.Create(Core30OverwriteForm);
      PathField.Parent := Core30OverwriteForm;
      PathField.SetBounds(Pad, ScaleY(185), Width - 2 * Pad, ScaleY(25));
      PathField.Font.Size := 9;
      PathField.Text := Lines[0];
      PathField.ReadOnly := True;
      Lines.Delete(0);
      if Lines.Count > 0 then begin
        Files := TNewMemo.Create(Core30OverwriteForm);
        Files.Parent := Core30OverwriteForm;
        Files.SetBounds(Pad, ScaleY(221), Width - 2 * Pad, ScaleY(81));
        Files.Font.Size := 9;
        Files.ReadOnly := True;
        Files.ScrollBars := ssBoth;
        Files.WordWrap := False;
        Files.Lines.Assign(Lines);
      end else Core30OverwriteForm.ClientHeight := ScaleY(278);
    end;
    ButtonsY := Core30OverwriteForm.ClientHeight - ScaleY(53);
    BackButton := Core30NativeButton(Core30OverwriteForm, Core30LabelText('返回', 'Back'),
      Width - Pad - ScaleX(230), ButtonsY, ScaleX(100), ScaleY(38), Core30ButtonQuiet, nil);
    BackButton.Cancel := True;
    BackButton.ModalResult := mrCancel;
    Proceed := Core30NativeButton(Core30OverwriteForm, Core30LabelText('确认覆盖', 'Replace'),
      Width - Pad - ScaleX(120), ButtonsY, ScaleX(120), ScaleY(38), Core30ButtonPrimary, nil);
    Proceed.ModalResult := mrOk;
    Core30OverwriteForm.ActiveControl := BackButton;
    Result := Core30OverwriteForm.ShowModal = mrOk;
  finally
    Lines.Free;
    Core30OverwriteForm.Free;
    Core30OverwriteForm := nil;
  end;
end;

function Core30FolderError: String;
var Folder: String;
begin
  Folder := Trim(WizardForm.DirEdit.Text);
  Result := '';
  if (Length(Folder) < 3) or (Copy(Folder, 2, 2) <> ':\') or
    (Pos('%', Folder) > 0) or (Pos('"', Folder) > 0) then
    Result := Core30LabelText('请选择本机磁盘目录，路径中不能含 % 或引号。', 'Choose a local folder without percent signs or quotes.')
  else if Pos('pmltrigger', Lowercase(ExtractFileName(RemoveBackslashUnlessRoot(Folder)))) = 0 then
    Result := Core30LabelText('安装目录名需包含 PmlTrigger。可点击「选择目录」自动生成。',
      'The final folder name must contain PmlTrigger. Browse to add it automatically.');
end;

procedure Core30UpdateInstall;
var FolderError: String;
begin
  if Core30Preparing then Exit;
  FolderError := Core30FolderError;
  Core30FolderHint.Font.Color := {#Core30Muted};
  if FolderError <> '' then begin
    Core30FolderHint.Font.Color := {#Core30Danger};
    Core30FolderHint.Caption := FolderError;
  end else
    Core30FolderHint.Caption := Core30LabelText('开始后将检查目录和待更新文件。',
      'Setup checks the folder and files when you continue.');
  if WizardForm.CurPageID = wpSelectDir then
    WizardForm.NextButton.Enabled := Core30AcceptedTerms and (FolderError = '');
end;

procedure Core30Preparation(Busy, CanCancel: Boolean; MessageText: String);
begin
  Core30Preparing := Busy;
  WizardForm.DirEdit.Enabled := not Busy;
  WizardForm.DirBrowseButton.Enabled := not Busy;
  Core30Terms.Visible := not Busy;
  Core30TermsLink.Visible := not Busy;
  Core30PrepareCancel.Visible := Busy;
  Core30PrepareCancel.Enabled := CanCancel;
  if Busy then begin
    Core30FolderHint.Caption := MessageText;
    WizardForm.NextButton.Enabled := False;
  end else begin
    Core30UpdateInstall;
    if MessageText <> '' then Core30FolderHint.Caption := MessageText;
  end;
end;

procedure Core30DirectoryChanged(Sender: TObject);
begin
  Core30UpdateInstall;
end;

procedure Core30BrowseDirectory(Sender: TObject);
var Folder: String;
begin
  Folder := WizardForm.DirEdit.Text;
  if not BrowseForFolder(Core30LabelText('选择安装目录','Choose the installation folder'), Folder, True) then Exit;
  if Pos('pmltrigger', Lowercase(ExtractFileName(RemoveBackslashUnlessRoot(Folder)))) = 0 then
    Folder := AddBackslash(Folder) + 'PmlTrigger.Yuzuha';
  WizardForm.DirEdit.Text := Folder;
  Core30UpdateInstall;
end;

procedure Core30TermsChanged(Sender: TObject);
begin
  WizardForm.LicenseAcceptedRadio.Checked := Core30AcceptedTerms;
  Core30UpdateInstall;
end;

function Core30StandardEntry: Boolean;
begin
  Result := not WizardSilent and (ExpandConstant('{param:PLAN|}') = '') and
    (ExpandConstant('{param:CONFIGUREAVEVA|0}') <> '1');
end;

procedure Core30IntroArtwork(Page: TNewNotebookPage; X, Y, W, H: Integer);
var Art: TBitmapImage;
begin
  ExtractTemporaryFile('core30-setup-art.png');
  Art := Core30Image(Page, X, Y, W, H);
  Art.Stretch := True;
  Art.PngImage.LoadFromFile(ExpandConstant('{tmp}\core30-setup-art.png'));
end;

procedure Core30CreateCompactPage(Product, ProductDescription: String);
var Page: TNewNotebookPage; Inset, ArtW, HeroH, DirY, RowY, RowH, HintY, BrowseW, FooterY: Integer;
    Heading, Description: TNewStaticText;
begin
  Page := WizardForm.SelectDirPage;
  Core30PageBackground(Page, {#Core30Canvas});
  Inset := ScaleX({#Core30CardInset});
  ArtW := ScaleX(248);
  HeroH := ScaleY(170);
  Core30Card(Page, 0, 0, Core30PaneW, HeroH, {#Core30Hero}, {#Core30HeroEdge});
  Core30IntroArtwork(Page, Core30PaneW - ArtW - ScaleX(10), ScaleY(8), ArtW, HeroH - ScaleY(16));

  Heading := Core30Text(Page, Inset, ScaleY(44), Core30PaneW - ArtW - Inset - ScaleX(6), ScaleY(51),
    {#Core30TitlePt}, Core30LabelText('安装 ' + Product, 'Install ' + Product), {#Core30Ink}, True);
  Heading.BringToFront;
  Description := Core30Text(Page, Inset, ScaleY(101), Core30PaneW - ArtW - Inset - ScaleX(6), ScaleY(53),
    {#Core30LabelPt}, ProductDescription + #13#10 +
    Core30LabelText('安装完成后，可在 PmlTwin 中配置 AVEVA。', 'Configure AVEVA from PmlTwin after setup.'),
    {#Core30Muted}, False);
  Description.WordWrap := True;

  WizardForm.SelectDirBitmapImage.Visible := False;
  WizardForm.SelectDirLabel.Visible := False;
  WizardForm.SelectDirBrowseLabel.Visible := False;
  WizardForm.DiskSpaceLabel.Visible := False;
  DirY := HeroH + ScaleY({#Core30Gap});
  RowY := DirY + ScaleY(42);
  WizardForm.DirEdit.Font.Size := {#Core30LabelPt};
  WizardForm.DirEdit.AutoSize := True;
  RowH := WizardForm.DirEdit.Height;
  HintY := RowY + RowH + ScaleY({#Core30Gap});
  Core30Card(Page, 0, DirY, Core30PaneW, HintY - DirY + ScaleY(33), {#Core30Surface}, {#Core30Border});
  Heading := Core30Text(Page, Inset, DirY + ScaleY(13), Core30PaneW - 2 * Inset, ScaleY(26),
    {#Core30LabelPt}, Core30LabelText('安装目录', 'Installation folder'), {#Core30Ink}, True);
  Heading.FocusControl := WizardForm.DirEdit;
  BrowseW := ScaleX(100);
  WizardForm.DirEdit.Anchors := [akLeft, akTop];
  WizardForm.DirEdit.AutoSelect := False;
  WizardForm.DirEdit.SetBounds(Inset, RowY, Core30PaneW - 2 * Inset - BrowseW - ScaleX({#Core30Gap}), RowH);
  WizardForm.DirEdit.BringToFront;
  WizardForm.DirBrowseButton.Anchors := [akLeft, akTop];
  WizardForm.DirBrowseButton.SetBounds(Core30PaneW - Inset - BrowseW, RowY, BrowseW, RowH);
  WizardForm.DirBrowseButton.Caption := Core30LabelText('选择目录…', 'Browse…');
  WizardForm.DirBrowseButton.OnClick := @Core30BrowseDirectory;
  WizardForm.DirBrowseButton.BringToFront;
  Core30StyleButton(WizardForm.DirBrowseButton, Core30ButtonQuiet);
  Core30FolderHint := Core30Text(Page, Inset, HintY, Core30PaneW - 2 * Inset, ScaleY(23),
    9, '', {#Core30Muted}, False);
  WizardForm.DirEdit.OnChange := @Core30DirectoryChanged;

  FooterY := WizardForm.ClientHeight - ScaleY(61);
  Core30Terms := TNewCheckBox.Create(WizardForm);
  Core30Terms.Parent := WizardForm;
  Core30Terms.SetBounds(Core30PadX + ScaleX(2), FooterY + ScaleY(9),
    ScaleX(244), ScaleY(29));
  Core30Terms.Font.Size := {#Core30LabelPt};
  Core30Terms.Caption := Core30LabelText('我已阅读并同意 Apache 2.0', 'I accept Apache License 2.0');
  Core30Terms.Checked := False;
  Core30Terms.OnClick := @Core30TermsChanged;
  Core30TermsLink := Core30NativeButton(WizardForm, Core30LabelText('查看协议', 'Read license'),
    Core30PadX + ScaleX(250), FooterY + ScaleY(6), ScaleX(91), ScaleY(36), Core30ButtonLink, @Core30TermsDocument);
  Core30PrepareCancel := Core30NativeButton(WizardForm, Core30LabelText('取消准备', 'Cancel preparation'),
    Core30PadX, FooterY, ScaleX(139), ScaleY(45), Core30ButtonQuiet, nil);
  Core30PrepareCancel.Visible := False;
end;

procedure Core30StyleCompactProgress(Product: String);
var Page: TNewNotebookPage; Inset, Y: Integer;
begin
  Page := WizardForm.InstallingPage;
  Core30PageBackground(Page, {#Core30Canvas});
  Inset := ScaleX({#Core30CardInset});
  Y := ScaleY(52);
  Core30Card(Page, 0, Y, Core30PaneW, ScaleY(188), {#Core30Surface}, {#Core30Border});
  Core30Text(Page, Inset, Y + ScaleY(21), Core30PaneW - 2 * Inset, ScaleY(41),
    {#Core30SectionPt}, Core30LabelText('正在安装 ' + Product, 'Installing ' + Product), {#Core30Ink}, True);
  WizardForm.StatusLabel.AutoSize := False;
  WizardForm.StatusLabel.SetBounds(Inset, Y + ScaleY(69), Core30PaneW - 2 * Inset, ScaleY(25));
  Core30TextStyle(WizardForm.StatusLabel, {#Core30LabelPt}, {#Core30Muted}, False);
  WizardForm.ProgressGauge.SetBounds(Inset, Y + ScaleY(107), Core30PaneW - 2 * Inset, ScaleY(15));
  WizardForm.FilenameLabel.AutoSize := False;
  WizardForm.FilenameLabel.SetBounds(Inset, Y + ScaleY(137), Core30PaneW - 2 * Inset, ScaleY(25));
  Core30TextStyle(WizardForm.FilenameLabel, 9, {#Core30Muted}, False);
end;

procedure Core30StyleIntro(Page: TNewNotebookPage; Title, Details: TNewStaticText; Finished: Boolean);
var Inset, W: Integer;
begin
  Core30PageBackground(Page, {#Core30Canvas});
  Core30Card(Page, 0, 0, Core30PaneW, Core30PaneH, {#Core30Surface}, {#Core30Border});
  Inset := ScaleX({#Core30CardInset});
  W := Core30PaneW - 2 * Inset;
  Title.AutoSize := False;
  Title.WordWrap := True;
  Title.SetBounds(Inset, ScaleY(34), W, ScaleY(62));
  Core30TextStyle(Title, {#Core30TitlePt}, {#Core30Ink}, True);
  Title.BringToFront;

  Details.AutoSize := False;
  Details.WordWrap := True;
  Details.SetBounds(Inset, ScaleY(109), W, Core30PaneH - ScaleY(128));
  Core30TextStyle(Details, {#Core30LabelPt}, {#Core30Muted}, False);
  Details.BringToFront;
  if Finished and Core30Compact then begin
    Title.Caption := Core30LabelText('安装完成', 'Installation complete');
    Title.Font.Color := {#Core30Success};
    Details.Caption := Core30LabelText('在 PmlTwin 中部署共用 PML 资源，并预览各版本的 EVARS 接入。',
      'Deploy shared PML resources in PmlTwin and review EVARS for the detected versions.');
    Details.Height := ScaleY(54);
  end;
end;

procedure Core30StyleInnerPages;
var Inset, Index, HeaderHeight: Integer;
begin
  Inset := ScaleX({#Core30Inset});
  WizardForm.InnerNotebook.Anchors := [akLeft, akTop];
  Core30PageBackground(WizardForm.InnerPage, {#Core30Canvas});
  WizardForm.MainPanel.StyleElements := [];
  WizardForm.MainPanel.ParentBackground := False;
  WizardForm.MainPanel.Color := {#Core30Hero};
  HeaderHeight := ScaleY(72);
  WizardForm.MainPanel.SetBounds(0, 0, Core30PaneW, HeaderHeight);
  WizardForm.PageNameLabel.AutoSize := False;
  WizardForm.PageNameLabel.SetBounds(Inset, ScaleY(10), Core30PaneW - 2 * Inset, ScaleY(31));
  Core30TextStyle(WizardForm.PageNameLabel, {#Core30SectionPt}, {#Core30Ink}, True);
  WizardForm.PageDescriptionLabel.AutoSize := False;
  WizardForm.PageDescriptionLabel.WordWrap := True;
  WizardForm.PageDescriptionLabel.SetBounds(Inset, ScaleY(39), Core30PaneW - 2 * Inset, ScaleY(32));
  Core30TextStyle(WizardForm.PageDescriptionLabel, {#Core30LabelPt}, {#Core30Muted}, False);
  if Core30Compact then
    WizardForm.InnerNotebook.SetBounds(0, 0, Core30PaneW, Core30PaneH)
  else
    WizardForm.InnerNotebook.SetBounds(Inset, HeaderHeight + ScaleY({#Core30Gap}), Core30PaneW - 2 * Inset,
      Core30PaneH - HeaderHeight - ScaleY({#Core30Gap}));
  for Index := 0 to WizardForm.InnerNotebook.PageCount - 1 do
    Core30PageBackground(WizardForm.InnerNotebook.Pages[Index], {#Core30Canvas});
end;

procedure Core30InitializeLightSkin(Product, Subtitle, Version, ProductDescription: String; Compact: Boolean);
var ButtonsTop, Right: Integer; FrameSurface: TBitmapImage;
begin
  Core30Compact := Compact;
  if WizardSilent then Exit;
  Core30TitleHeight := ScaleY(50);
  Core30PadX := ScaleX({#Core30CardInset});
  Core30AttachFrame(WizardForm, Core30TitleHeight);
  if Compact then begin
    WizardForm.ClientWidth := ScaleX(710);
    WizardForm.ClientHeight := ScaleY(414);
  end;
  FrameSurface := Core30Image(WizardForm, 0, 0, WizardForm.ClientWidth, WizardForm.ClientHeight);
  Core30Fill(FrameSurface, {#Core30Canvas}, {#Core30Canvas}, {#Core30BorderStrong}, {#Core30CardRadius});
  FrameSurface.SendToBack;
  Core30PaneW := WizardForm.ClientWidth - 2 * Core30PadX;
  Core30PaneH := WizardForm.ClientHeight - Core30TitleHeight - ScaleY(73);
  WizardForm.OuterNotebook.SetBounds(Core30PadX, Core30TitleHeight, Core30PaneW, Core30PaneH);
  WizardForm.WizardBitmapImage.Visible := False;
  WizardForm.WizardBitmapImage2.Visible := False;
  WizardForm.WizardSmallBitmapImage.Visible := False;
  WizardForm.Bevel.Visible := False;
  WizardForm.Bevel1.Visible := False;
  WizardForm.BeveledLabel.Visible := False;
  Core30Title(WizardForm, Subtitle + '  ·  ' + Version, @Core30CloseWizard, True);
  Core30StyleInnerPages;
  Core30StyleIntro(WizardForm.WelcomePage, WizardForm.WelcomeLabel1, WizardForm.WelcomeLabel2, False);
  Core30StyleIntro(WizardForm.FinishedPage, WizardForm.FinishedHeadingLabel, WizardForm.FinishedLabel, True);

  Core30InstallButtonWidth := ScaleX(146);
  ButtonsTop := WizardForm.ClientHeight - ScaleY(61);
  Right := WizardForm.ClientWidth - Core30PadX;
  WizardForm.NextButton.SetBounds(Right - Core30InstallButtonWidth, ButtonsTop, Core30InstallButtonWidth, ScaleY(45));
  WizardForm.NextButton.Font.Size := {#Core30LabelPt};
  WizardForm.NextButton.Font.Style := [fsBold];
  Core30StyleButton(WizardForm.NextButton, Core30ButtonPrimary);
  WizardForm.BackButton.SetBounds(WizardForm.NextButton.Left - ScaleX(117), ButtonsTop, ScaleX(107), ScaleY(45));
  Core30StyleButton(WizardForm.BackButton, Core30ButtonQuiet);
  WizardForm.CancelButton.SetBounds(Core30PadX, ButtonsTop, ScaleX(104), ScaleY(45));
  Core30StyleButton(WizardForm.CancelButton, Core30ButtonQuiet);

  if Compact then begin
    Core30CreateCompactPage(Product, ProductDescription);
    Core30StyleCompactProgress(Product);
    WizardForm.KeyPreview := True;
    WizardForm.OnKeyDown := @Core30Escape;
  end;
end;

procedure Core30LightPageChanged(PageID: Integer);
var DirectoryPage: Boolean;
begin
  if WizardSilent or (Core30TitleClose = nil) then Exit;
  Core30TitleClose.Enabled := WizardForm.CancelButton.Enabled or (PageID = wpFinished);
  if not Core30Compact then Exit;
  DirectoryPage := PageID = wpSelectDir;
  WizardForm.MainPanel.Visible := (PageID = wpPreparing);
  if PageID = wpPreparing then begin
    WizardForm.InnerNotebook.SetBounds(0, WizardForm.MainPanel.Height + ScaleY({#Core30Gap}), Core30PaneW,
      Core30PaneH - WizardForm.MainPanel.Height - ScaleY({#Core30Gap}));
  end else begin
    WizardForm.InnerNotebook.SetBounds(0, 0, Core30PaneW, Core30PaneH);
  end;
  Core30Terms.Visible := DirectoryPage and not Core30Preparing;
  Core30TermsLink.Visible := DirectoryPage and not Core30Preparing;
  WizardForm.BackButton.Visible := False;
  WizardForm.CancelButton.Visible := False;
  if DirectoryPage then begin
    WizardForm.NextButton.Caption := Core30LabelText('安装', 'Install');
    Core30UpdateInstall;
  end;
  if PageID = wpFinished then begin
    WizardForm.RunList.SetBounds(ScaleX({#Core30CardInset}), ScaleY(173),
      Core30PaneW - 2 * ScaleX({#Core30CardInset}), ScaleY(59));
    WizardForm.RunList.Color := {#Core30Surface};
    WizardForm.RunList.Font.Size := {#Core30LabelPt};
    WizardForm.RunList.BringToFront;
  end;
end;

procedure Core30StyleCustomPage(Page: TWizardPage);
begin
  if not WizardSilent then Core30PageBackground(Page.Surface, {#Core30Canvas});
end;
