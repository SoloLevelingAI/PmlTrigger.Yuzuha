using System.IO;
using System.Text.Json;

namespace YuzuhaToolkit.Desktop;

/// <summary>独立保存显示偏好，切换主题不提交启动草稿或函数权限。</summary>
internal static class DesktopDisplaySettings
{
    /// <summary>读取本机明暗偏好；首次启动为明亮模式，损坏文件由界面报告。</summary>
    internal static string ReadTheme(string stateDirectory)
    {
        var file = Path.Combine(stateDirectory, "desktop-display.json");
        if (!File.Exists(file)) return "Light";
        using var data = JsonDocument.Parse(File.ReadAllText(file));
        return data.RootElement.TryGetProperty("theme", out var value) && value.GetString() == "Dark" ? "Dark" : "Light";
    }
    /// <summary>原子保存用户明确选择的主题，不修改工作区或工程。</summary>
    internal static void SaveTheme(string stateDirectory, string theme)
    {
        if (theme is not ("Light" or "Dark")) throw new ArgumentException("Unknown desktop theme.", nameof(theme));
        Directory.CreateDirectory(stateDirectory);
        var file = Path.Combine(stateDirectory, "desktop-display.json"); var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, "{\"theme\":\"" + theme + "\"}"); File.Move(temporary, file, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
