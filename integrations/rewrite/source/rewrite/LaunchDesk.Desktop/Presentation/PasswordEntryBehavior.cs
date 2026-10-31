using System.Windows.Threading;

namespace LaunchDesk.Desktop;

/// <summary>管理本次输入的显示缓冲、一次性移交及销毁。</summary>
public sealed class PasswordEntryBehavior : ObservableObject, IDisposable
{
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private TransientSecret? input;
    private bool revealed;
    private bool disposed;
    /// <summary>是否正在按住显示本次输入。</summary>
    public bool IsRevealed => revealed;
    /// <summary>当前编辑会话是否持有未移交的临时秘密。</summary>
    public bool HasInput => input is not null;
    /// <summary>只在明确显示期间取得的本次输入；隐藏时为空。</summary>
    public string DisplayText => revealed && input is not null ? new string(input.Borrow()) : "";
    /// <summary>临时输入被移交或清除时通知输入控件清空可视内容。</summary>
    public event Action? InputCleared;

    /// <summary>复制本次编辑的秘密字符，替换并清理旧临时缓冲区。</summary>
    /// <include file="../../Comments.xml" path="doc/Value/*"/>
    public void AttachInput(ReadOnlySpan<char> value)
    {
        dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(disposed, this);
        input?.Dispose();
        input = value.Length == 0 ? null : new TransientSecret(value);
        Notify(nameof(HasInput), nameof(DisplayText));
    }

    /// <summary>仅在有效编辑会话按用户动作短暂显示本次输入；显隐按钮获焦不销毁输入；显示结束、离开显隐交互或编辑页失焦时调用HideDisplay。</summary>
    public void RevealWhileEditing()
    {
        dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(disposed, this);
        revealed = input is not null;
        Notify(nameof(IsRevealed), nameof(DisplayText));
    }

    /// <summary>立即隐藏明文、清理显示缓冲，保留控件内本次未移交输入。密码框移焦至保存按钮/其他字段/显隐按钮，或窗口暂时停用，均调用。</summary>
    public void HideDisplay()
    {
        dispatcher.VerifyAccess();
        revealed = false;
        Notify(nameof(IsRevealed), nameof(DisplayText));
    }

    /// <summary>在Dispatcher取得本次输入的唯一TransientSecret所有权后，标记已消费并清空控件及显示缓冲。同一输入代次仅移交一次；清空后的空状态不能推断为Keep/Clear。</summary>
    /// <returns>移交唯一临时秘密，由调用方释放；输入已移交或为空则抛异常。</returns>
    public TransientSecret DetachInput()
    {
        dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(disposed, this);
        var transferred = input ?? throw new InvalidOperationException("本次输入已经移交或为空。");
        input = null;
        HideDisplay();
        Raise(nameof(HasInput));
        InputCleared?.Invoke();
        return transferred;
    }

    /// <summary>显式取消编辑、离页、保存尝试结束或销毁控件时，隐藏并销毁未移交输入和显示缓冲；普通页内移焦不调用。</summary>
    public void HideAndClear()
    {
        dispatcher.VerifyAccess();
        input?.Dispose();
        input = null;
        HideDisplay();
        Raise(nameof(HasInput));
        InputCleared?.Invoke();
    }

    /// <summary>在所属 Dispatcher 上隐藏并销毁未移交的输入，结束当前编辑行为。</summary>
    public void Dispose()
    {
        if (disposed) return;
        HideAndClear();
        disposed = true;
    }
}