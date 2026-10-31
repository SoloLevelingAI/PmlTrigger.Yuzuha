using Microsoft.Win32;

namespace LaunchDesk.Desktop;

/// <summary>提供 Windows 文件选择、目录选择和明确确认对话框。</summary>
public sealed class PresentationDialogs : IPresentationDialogs
{
    /// <summary>请求用户选择单个本机文件；取消时返回空值。</summary>
    /// <inheritdoc/>
    public string? OpenFile(string title, string filter = "所有文件|*.*")
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>请求用户选择普通文件集合；取消时返回空集合。</summary>
    /// <inheritdoc/>
    public string[] OpenFiles(string title)
    {
        var dialog = new OpenFileDialog { Title = title, Multiselect = true };
        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }

    /// <summary>请求用户明确选择分享文件保存位置；取消时返回空值。</summary>
    /// <inheritdoc/>
    public string? SaveFile(string title, string suggestedName)
    {
        var dialog = new SaveFileDialog
            { Title = title, FileName = suggestedName, Filter = "JSON 配置|*.json", DefaultExt = ".json" };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>请求用户选择本机目录；取消时返回空值。</summary>
    /// <inheritdoc/>
    public string? OpenFolder(string title)
    {
        var dialog = new OpenFolderDialog { Title = title };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    /// <summary>固定用户明确确认时的计划及其摘要，或等待对话框的明确确认。</summary>
    /// <inheritdoc/>
    public bool Confirm(string message, string title)
    {
        return MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.OKCancel,
            MessageBoxImage.Question) == MessageBoxResult.OK;
    }
}