const
  Core30ButtonQuiet = 0;
  Core30ButtonPrimary = 1;
  Core30ButtonMinimize = 2;
  Core30ButtonClose = 3;
  Core30ButtonLink = 4;

type
  TCore30Palette = record
    Canvas, Surface, Ink, Muted, Primary, Hover, OnPrimary: Integer;
    Selected, Border, BorderStrong, Danger, DangerBg: Integer;
  end;

var
  Core30FrameAvailable: Boolean;

procedure Core30SetPalette(var Palette: TCore30Palette);
  external 'Core30SetPalette@files:Core30Frame.dll stdcall setuponly delayload';
function Core30BindFrame(Window: HWND; TitleHeight, Exclusion, Diameter: Integer): BOOL;
  external 'Core30BindFrame@files:Core30Frame.dll stdcall setuponly delayload';
function Core30BindCaption(Window: HWND): BOOL;
  external 'Core30BindCaption@files:Core30Frame.dll stdcall setuponly delayload';
function Core30BindButton(Window: HWND; Role, Diameter, FocusInset: Integer): BOOL;
  external 'Core30BindButton@files:Core30Frame.dll stdcall setuponly delayload';
function Core30ShowWindow(Window: HWND; Command: Integer): BOOL;
  external 'ShowWindow@user32.dll stdcall';
function Core30SendMessage(Window: HWND; Message: UINT; WParam: WPARAM; LParam: LPARAM): LRESULT;
  external 'SendMessageW@user32.dll stdcall';

procedure Core30AttachFrame(Form: TSetupForm; TitleHeight: Integer);
var Palette: TCore30Palette; OldBorder: TFormBorderStyle;
begin
  Palette.Canvas := {#Core30Canvas};
  Palette.Surface := {#Core30Surface};
  Palette.Ink := {#Core30Ink};
  Palette.Muted := {#Core30Muted};
  Palette.Primary := {#Core30Primary};
  Palette.Hover := {#Core30PrimaryHover};
  Palette.OnPrimary := {#Core30OnPrimary};
  Palette.Selected := {#Core30Selected};
  Palette.Border := {#Core30Border};
  Palette.BorderStrong := {#Core30BorderStrong};
  Palette.Danger := {#Core30Danger};
  Palette.DangerBg := {#Core30DangerBg};
  OldBorder := Form.BorderStyle;
  try
    Core30SetPalette(Palette);
    Form.BorderStyle := bsNone;
    Form.StyleElements := [];
    Form.Color := {#Core30Canvas};
    Core30FrameAvailable := Core30BindFrame(Form.Handle, TitleHeight, ScaleX(104), ScaleX(2 * {#Core30CardRadius}));
  except
    Core30FrameAvailable := False;
    Log(GetExceptionMessage);
  end;
  if not Core30FrameAvailable then Form.BorderStyle := OldBorder;
end;

procedure Core30StyleButton(Button: TNewButton; Role: Integer);
begin
  if not Core30FrameAvailable then Exit;
  Button.StyleElements := [];
  Core30BindButton(Button.Handle, Role, ScaleX(2 * {#Core30ControlRadius}), ScaleX(4));
end;
