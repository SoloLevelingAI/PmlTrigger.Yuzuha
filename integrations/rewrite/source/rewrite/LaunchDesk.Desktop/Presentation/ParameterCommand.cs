namespace LaunchDesk.Desktop;

/// <summary>只接受指定类型命令参数的同步页面命令。</summary>
/// <typeparam name="T"><include file="../../Comments.xml" path="doc/T/typeparam[@name='T']/*"/></typeparam>
/// <param name="action">命令的同步/异步操作。</param>
public sealed class ParameterCommand<T>(Action<T> action) : ICommand
{
    /// <summary>检查 WPF 命令参数是否符合该命令的类型约束。</summary>
    /// <param name="parameter">WPF 绑定传入的命令参数。</param>
    /// <returns>参数是 T 类型的实例时为 true。</returns>
    public bool CanExecute(object? parameter) => parameter is T;
    /// <summary>参数符合 T 类型时转交给页面回调，否则忽略本次调用。</summary>
    /// <param name="parameter">准备传入回调的 WPF 命令参数。</param>
    public void Execute(object? parameter) { if (parameter is T value) action(value); }
    /// <summary>可执行状态变化时通知WPF重评按钮。</summary>
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
