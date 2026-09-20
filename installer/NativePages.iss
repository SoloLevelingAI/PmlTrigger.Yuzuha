// All visible configuration controls belong to the Inno wizard.
// The helper is headless; it only discovers, validates and applies plans.
var
  ProductPage: TWizardPage;
  AIPage: TInputQueryWizardPage;
  ModePage: TInputOptionWizardPage;
  Products: TNewCheckListBox;
  FileEdit: TNewEdit;
  ProfileBox, EncodingBox: TNewComboBox;
  Details: TNewMemo;
  Paths, Profiles, Encodings, Evidence, NoInit, Multiple: TStringList;
  CurrentProduct: Integer;
  LoadingProduct, Discovered: Boolean;

function JsonString(Value: String): String;
var I: Integer; C: String;
begin
  Result := '"';
  for I := 1 to Length(Value) do begin
    C := Copy(Value,I,1);
    if C = '\' then Result := Result + '\\'
    else if C = '"' then Result := Result + '\"'
    else if Ord(Value[I]) < 32 then Result := Result + '\u00' +
      Copy('0123456789abcdef',(Ord(Value[I]) div 16)+1,1) + Copy('0123456789abcdef',(Ord(Value[I]) mod 16)+1,1)
    else Result := Result + C;
  end;
  Result := Result + '"';
end;

procedure EditProduct(Sender: TObject);
begin
  if LoadingProduct or (CurrentProduct < 0) then Exit;
  if Paths[CurrentProduct] <> FileEdit.Text then NoInit[CurrentProduct] := '0';
  Paths[CurrentProduct] := FileEdit.Text;
  Profiles[CurrentProduct] := ProfileBox.Text;
  Encodings[CurrentProduct] := EncodingBox.Text;
end;

procedure SelectProduct(Sender: TObject);
begin
  CurrentProduct := Products.ItemIndex;
  if CurrentProduct < 0 then Exit;
  LoadingProduct := True;
  FileEdit.Text := Paths[CurrentProduct];
  ProfileBox.ItemIndex := ProfileBox.Items.IndexOf(Profiles[CurrentProduct]);
  EncodingBox.ItemIndex := EncodingBox.Items.IndexOf(Encodings[CurrentProduct]);
  Details.Text := Evidence[CurrentProduct] + #13#10 + Paths[CurrentProduct];
  LoadingProduct := False;
end;

procedure AddProduct(Product, Path, Profile, Source, InitAbsent, Ambiguous: String);
begin
  Products.AddCheckBox(Product, '', 0, False, True, False, False, nil);
  Paths.Add(Path); Profiles.Add(Profile); Encodings.Add('auto');
  Evidence.Add(Source); NoInit.Add(InitAbsent); Multiple.Add(Ambiguous);
end;

procedure DiscoverProducts(Sender: TObject);
var Error, Ini, Section, Path: String; I, Count: Integer;
begin
  ExtractSupport;
  Ini := ExpandConstant('{tmp}\discovery.ini');
  Error := Bridge(ExpandConstant('{tmp}\Yuzuha.SetupBridge.exe'),'discover-native',Q(Ini),SW_HIDE);
  if Error <> '' then begin MsgBox(Error,mbError,MB_OK); Exit; end;
  Count := GetIniInt('discovery','count',0,0,10000,Ini);
  for I := 0 to Count-1 do begin
    Section := 'row' + IntToStr(I);
    Path := GetIniString(Section,'path','',Ini);
    if (Path = '') or (Paths.IndexOf(Path) < 0) then
      AddProduct(GetIniString(Section,'product','',Ini),Path,
        GetIniString(Section,'profile','',Ini),GetIniString(Section,'evidence','',Ini),
        GetIniString(Section,'noInit','0',Ini),GetIniString(Section,'multiple','0',Ini));
  end;
  Details.Text := T('扫描完成。勾选需要接入的环境；编码默认保留原文件。','Scan complete. Select environments to connect; original encoding is preserved.') + #13#10 +
    GetIniString('discovery','warning','',Ini);
  Discovered := True;
end;

procedure BrowseEnvironment(Sender: TObject);
var Path: String;
begin
  Path := FileEdit.Text;
  if GetOpenFileName(T('选择 EVARS.INIT / BAT','Select EVARS.INIT / BAT'),Path,'',
    'AVEVA environment|*.init;*.bat|All files|*.*','') then begin
    if CurrentProduct < 0 then Exit;
    FileEdit.Text := Path;
  end;
end;

procedure AddManualProduct(Sender: TObject);
var Path: String;
begin
  Path := '';
  if not GetOpenFileName(T('添加配置文件','Add environment file'),Path,'',
    'AVEVA environment|*.init;*.bat|All files|*.*','') then Exit;
  if Paths.IndexOf(Path) >= 0 then begin Products.ItemIndex := Paths.IndexOf(Path); SelectProduct(nil); Exit; end;
  AddProduct(T('手动：','Manual: ') + ExtractFileName(Path),Path,'',T('手动选择，请指定 Host Profile','Manual selection: choose Host Profile'),'0','0');
  Products.ItemIndex := Products.Items.Count-1;
  SelectProduct(nil);
end;

procedure BrowseMcp(Sender: TObject);
var Path: String;
begin
  Path := AIPage.Values[0];
  if GetOpenFileName(T('选择客户端 JSON 配置','Select client JSON configuration'),Path,'','JSON|*.json','json') then AIPage.Values[0] := Path;
end;

procedure BrowseSkill(Sender: TObject);
var Path: String;
begin
  Path := AIPage.Values[1];
  if BrowseForFolder(T('选择 skills 父目录，将添加 yuzuha-toolkit','Choose skills parent; yuzuha-toolkit will be appended'),Path,True) then
    AIPage.Values[1] := AddBackslash(Path) + 'yuzuha-toolkit';
end;

procedure NativeButton(Page: TWizardPage; X, Y, W: Integer; Caption: String; Handler: TNotifyEvent);
var Button: TNewButton;
begin
  Button := TNewButton.Create(Page); Button.Parent := Page.Surface;
  Button.SetBounds(ScaleX(X),ScaleY(Y),ScaleX(W),ScaleY(25));
  Button.Caption := Caption; Button.OnClick := Handler;
end;

procedure CreateNativePages;
var LabelControl: TNewStaticText; BrowseButton: TNewButton;
begin
  CurrentProduct := -1;
  Paths := TStringList.Create; Profiles := TStringList.Create; Encodings := TStringList.Create;
  Evidence := TStringList.Create; NoInit := TStringList.Create; Multiple := TStringList.Create;
  ModePage := CreateInputOptionPage(wpSelectDir,T('安装目标','Installation scope'),
    T('是否接入 AVEVA？','Connect AVEVA?'),T('文件安装完成不等于 AVEVA 已接入。','Installing files alone does not connect AVEVA.'),True,False);
  ModePage.Add(T('安装并接入 AVEVA（必须选择环境）','Install and connect AVEVA (environment selection required)'));
  ModePage.Add(T('仅安装文件 / AI 客户端，不修改 AVEVA','Files / AI client only; do not modify AVEVA'));
  ModePage.SelectedValueIndex := 0;
  ProductPage := CreateCustomPage(ModePage.ID,T('选择 AVEVA 环境','Select AVEVA environments'),
    T('勾选需要接入的产品，下方查看或调整配置。','Select products to connect; review settings below.'));
  NativeButton(ProductPage,0,0,150,T('重新探测注册表','Scan registry'),@DiscoverProducts);
  NativeButton(ProductPage,160,0,150,T('手动添加 INIT/BAT','Add INIT/BAT'),@AddManualProduct);
  Products := TNewCheckListBox.Create(ProductPage); Products.Parent := ProductPage.Surface;
  Products.SetBounds(0,ScaleY(32),ProductPage.SurfaceWidth,ScaleY(95));
  Products.OnClick := @SelectProduct;
  LabelControl := TNewStaticText.Create(ProductPage); LabelControl.Parent := ProductPage.Surface;
  LabelControl.SetBounds(0,ScaleY(133),ProductPage.SurfaceWidth,ScaleY(17));
  LabelControl.Caption := T('当前行：配置文件 / Host Profile / 编码','Current row: environment file / Host Profile / encoding');
  FileEdit := TNewEdit.Create(ProductPage); FileEdit.Parent := ProductPage.Surface;
  FileEdit.SetBounds(0,ScaleY(154),ProductPage.SurfaceWidth-ScaleX(90),ScaleY(23)); FileEdit.OnChange := @EditProduct;
  BrowseButton := TNewButton.Create(ProductPage); BrowseButton.Parent := ProductPage.Surface;
  BrowseButton.SetBounds(ProductPage.SurfaceWidth-ScaleX(85),ScaleY(153),ScaleX(85),ScaleY(25));
  BrowseButton.Caption := T('浏览…','Browse…'); BrowseButton.OnClick := @BrowseEnvironment;
  ProfileBox := TNewComboBox.Create(ProductPage); ProfileBox.Parent := ProductPage.Surface;
  ProfileBox.SetBounds(0,ScaleY(184),ScaleX(210),ScaleY(23)); ProfileBox.Style := csDropDownList;
  ProfileBox.Items.Add(''); ProfileBox.Items.Add('AM'); ProfileBox.Items.Add('PDMS');
  ProfileBox.Items.Add('E3D2.1'); ProfileBox.Items.Add('E3D3.1.0'); ProfileBox.Items.Add('E3D3.1.6'); ProfileBox.OnChange := @EditProduct;
  EncodingBox := TNewComboBox.Create(ProductPage); EncodingBox.Parent := ProductPage.Surface;
  EncodingBox.SetBounds(ScaleX(220),ScaleY(184),ScaleX(150),ScaleY(23)); EncodingBox.Style := csDropDownList;
  EncodingBox.Items.Add('auto'); EncodingBox.Items.Add('utf-8'); EncodingBox.Items.Add('system'); EncodingBox.OnChange := @EditProduct;
  Details := TNewMemo.Create(ProductPage); Details.Parent := ProductPage.Surface;
  Details.SetBounds(0,ScaleY(217),ProductPage.SurfaceWidth,ProductPage.SurfaceHeight-ScaleY(217));
  Details.ReadOnly := True; Details.ScrollBars := ssVertical;
  AIPage := CreateInputQueryPage(ProductPage.ID,T('可选：AI 客户端接入','Optional: AI client integration'),
    T('与 AVEVA 配置独立，可以全部留空','Independent of AVEVA; both fields may be left blank'),
    T('JSON 指客户端 MCP 配置文件；技能目标填写完整的 yuzuha-toolkit 目录。不会将 TOML 当成 JSON。','JSON means the client MCP configuration. Skill target is the full yuzuha-toolkit directory. TOML is not treated as JSON.'));
  AIPage.Add(T('MCP 配置文件（JSON）','MCP configuration (JSON)'),False);
  AIPage.Add(T('技能安装目标（完整目录）','Skill target (full directory)'),False);
  NativeButton(AIPage,0,200,160,T('浏览 JSON…','Browse JSON…'),@BrowseMcp);
  NativeButton(AIPage,170,200,180,T('选择 skills 父目录…','Choose skills parent…'),@BrowseSkill);
end;

function NativeRequest: String;
var I: Integer; Environments, Path, Mode: String; InitAbsent: Boolean;
begin
  Result := ''; Environments := '';
  Mode := 'aveva'; if ModePage.SelectedValueIndex = 1 then Mode := 'none';
  for I := 0 to Products.Items.Count-1 do if Products.Checked[I] and (Mode = 'aveva') then begin
    Path := Paths[I];
    if (Path = '') or (Profiles[I] = '') then begin
      MsgBox(T('请为勾选的环境指定配置文件和 Host Profile。','Select a configuration file and Host Profile for every checked environment.'),mbError,MB_OK); Exit;
    end;
    InitAbsent := NoInit[I] = '1';
    if (CompareText(ExtractFileExt(Path),'.bat') = 0) and not InitAbsent then begin
      if MsgBox(T('请确认该环境没有适用 INIT，实际使用以下 BAT：','Confirm this environment has no applicable INIT and uses this BAT:')+#13#10+Path,mbConfirmation,MB_YESNO) <> IDYES then Exit;
      InitAbsent := True;
    end;
    if Multiple[I] = '1' then
      if MsgBox(T('存在多个候选，请确认选择：','Multiple candidates; confirm selection:')+#13#10+Path,mbConfirmation,MB_YESNO) <> IDYES then Exit;
    if Environments <> '' then Environments := Environments + ',';
    Environments := Environments + '{"path":'+JsonString(Path)+',"profile":'+JsonString(Profiles[I])+
      ',"encoding":'+JsonString(Encodings[I])+',"confirmed":true,"noInitConfirmed":';
    if InitAbsent then Environments := Environments + 'true}' else Environments := Environments + 'false}';
  end;
  if (Mode = 'aveva') and (Environments = '') then begin
    MsgBox(T('尚未选择 AVEVA 环境。请返回选择，或明确改为仅安装文件。','No AVEVA environment selected. Go back to select one or explicitly choose files-only mode.'),mbError,MB_OK); Exit;
  end;
  Result := '{"integrationMode":'+JsonString(Mode)+',"root":'+JsonString(ExpandConstant('{app}'))+',"mcpJson":'+JsonString(Trim(AIPage.Values[0]))+
    ',"skillTarget":'+JsonString(Trim(AIPage.Values[1]))+',"environments":['+Environments+']}';
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := ((ExpandConstant('{param:PLAN|}') <> '') or WizardSilent) and
    ((PageID = ModePage.ID) or (PageID = ProductPage.ID) or (PageID = AIPage.ID));
  if (PageID = ProductPage.ID) and (ModePage.SelectedValueIndex = 1) then Result := True;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = ProductPage.ID) and not Discovered then DiscoverProducts(nil);
end;
