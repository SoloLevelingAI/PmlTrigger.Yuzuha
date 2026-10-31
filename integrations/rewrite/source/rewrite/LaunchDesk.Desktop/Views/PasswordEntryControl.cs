using System.Runtime.InteropServices;

namespace LaunchDesk.Desktop;

/// <summary>安全输入框与按住显示的遮罩；不回显已保存密码。</summary>
public sealed class PasswordEntryControl : Grid
{
    private readonly PasswordBox input = new();
    private readonly TextBox reveal = new() { IsReadOnly = true, Visibility = Visibility.Collapsed, IsTabStop = false };
    private readonly PasswordEntryBehavior behavior;
    private bool updating;

    /// <summary>创建密码输入与按住显示控件，连接当前编辑会话的临时秘密行为。</summary>
    /// <param name="behavior">本次编辑会话的秘密输入行为。</param>
    public PasswordEntryControl(PasswordEntryBehavior behavior)
    {
        this.behavior = behavior;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var eye = new Button
            { Content = new AppIcon { Kind="Eye",Width=16,Height=16 }, Width=34, Padding=new Thickness(6), Margin = new Thickness(7, 0, 0, 0), Focusable = false, ToolTip = "按住显示本次输入；松开或离开立即隐藏" };
        Children.Add(input);
        Children.Add(reveal);
        SetColumn(eye, 1);
        Children.Add(eye);
        input.PasswordChanged += (_, _) => Capture();
        eye.PreviewMouseLeftButtonDown += (_, e) =>
        {
            behavior.RevealWhileEditing();
            Show();
            eye.CaptureMouse();
            e.Handled = true;
        };
        eye.PreviewMouseLeftButtonUp += (_, e) =>
        {
            eye.ReleaseMouseCapture();
            Hide();
            input.Focus();
            e.Handled = true;
        };
        eye.LostMouseCapture += (_, _) => Hide();
        LostKeyboardFocus += (_, _) => Hide();
        Unloaded += (_, _) => Hide();
        behavior.InputCleared += () =>
        {
            updating = true;
            input.Clear();
            reveal.Clear();
            updating = false;
            Hide();
        };
        behavior.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(PasswordEntryBehavior.IsRevealed)
                or nameof(PasswordEntryBehavior.DisplayText)) Render();
        };
    }

    private void Capture()
    {
        if (updating) return;
        using var secure = input.SecurePassword;
        var pointer = Marshal.SecureStringToBSTR(secure);
        var characters = new char[secure.Length];
        try
        {
            Marshal.Copy(pointer, characters, 0, characters.Length);
            behavior.AttachInput(characters);
        }
        finally
        {
            Array.Clear(characters);
            Marshal.ZeroFreeBSTR(pointer);
        }
    }

    private void Show()
    {
        Render();
    }

    private void Hide()
    {
        behavior.HideDisplay();
    }

    private void Render()
    {
        reveal.Text = behavior.DisplayText;
        reveal.IsHitTestVisible = false;
        reveal.Visibility = behavior.IsRevealed ? Visibility.Visible : Visibility.Collapsed;
        // 安全输入被视觉遮盖时仍保留其键盘焦点。
        input.Opacity = behavior.IsRevealed ? 0 : 1;
    }
}
