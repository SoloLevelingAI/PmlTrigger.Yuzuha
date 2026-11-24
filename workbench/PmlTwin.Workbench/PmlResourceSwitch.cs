using System.IO;
using System.Text.RegularExpressions;
using Core30.Core;
using Core30.PmlTwin;
using YuzuhaToolkit.Deployment;

namespace PmlTwin.Workbench;

internal sealed record EvarsLine(int Number, string Text, bool Removed);
internal sealed class EvarsWriteException(string backup, Exception inner)
    : IOException($"{inner.Message}\n备份路径 {backup}", inner)
{
    public string Backup { get; } = backup;
}

internal sealed record PmlResourceSwitch(string Path, byte[] Original, byte[] Proposed, EvarsLine[] Lines)
{
    private const int MaxEvarsBytes = 1024 * 1024;
    private static readonly Regex Start = Marker(">>>");
    private static readonly Regex End = Marker("<<<");
    private static readonly Regex ManagedSetting = new(
        """^[ \t]*@?set[ \t]+(?<quote>"?)(?<name>pmllib|pmlui|pdmsui|Yuzuha|YuzuhaFramework|YUZUHA_STARTUP_ACTIONS|YUZUHA_PROGRAM_ROOT)=(?<value>[^"\r\n]*)\k<quote>[ \t]*$""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public bool Changed => !Original.AsSpan().SequenceEqual(Proposed);

    public static string LocalDirectory(string value)
    {
        var path = System.IO.Path.TrimEndingDirectorySeparator(LocalSource.RequireAbsolutePath(value.Trim()));
        if (path.IndexOfAny(['\r', '\n', '"', '%', '!', '&', '^', '|', '<', '>', ';']) >= 0)
            throw new InvalidDataException("目录含 EVARS 无法安全写入的批处理字符。");
        return path;
    }

    public static bool Overlaps(string first, string second) => Within(first, second) || Within(second, first);

    private static bool Within(string parent, string candidate)
    {
        var relative = System.IO.Path.GetRelativePath(parent, candidate);
        return !System.IO.Path.IsPathFullyQualified(relative) && relative != ".." &&
            !relative.StartsWith(".." + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    public static (string Library, string Interface) SharedDirectories(string root)
    {
        var shared = new FeatureContext(LocalDirectory(root));
        return (shared.FilePath("PMLLIB"), shared.FilePath("PMLUI"));
    }

    public static PmlResourceSwitch Preview(string evars, string program, string library, string ui, string product, string release)
    {
        var path = OpenEvars(evars);
        program = new FeatureContext(LocalDirectory(program)).Root;
        library = LocalDirectory(library);
        ui = LocalDirectory(ui);
        var sharedRoot = System.IO.Path.GetDirectoryName(library);
        var standardLayout = sharedRoot is not null && LocalSource.SamePath(library, System.IO.Path.Combine(sharedRoot, "PMLLIB")) &&
            LocalSource.SamePath(ui, System.IO.Path.Combine(sharedRoot, "PMLUI"));
        if (standardLayout) (library, ui) = SharedDirectories(sharedRoot!);
        else
        {
            library = new FeatureContext(library).Root;
            ui = new FeatureContext(ui).Root;
        }
        var programContainsSharedResources = standardLayout && LocalSource.SamePath(program, sharedRoot!);
        if (Overlaps(library, ui) || (!programContainsSharedResources && (Overlaps(program, library) || Overlaps(program, ui))))
            throw new InvalidDataException("请使用共用根目录下的 PMLLIB 和 PMLUI，或两个独立的部署目录。");
        var host = PmlHostConfiguration.AtProgramDirectory(program, product, release);
        if (host.DllPath.Length == 0)
            throw new InvalidDataException("所选程序目录缺少这个 AVEVA 产品使用的 Host，请选择已安装的 Phayton 程序目录。");

        var original = FileContent.ReadBounded(path, MaxEvarsBytes);
        var source = LocalText.Decode(original);
        RequireRoundTrip(original, source);
        var newline = source.Content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var (baseText, removed, startupActions) = RemoveOldBlock(source.Content);
        var proposed = EnvironmentTextEditor.Edit(baseText, program, host.Profile, host.Framework == "net35", newline,
            out _, startupActions, includeFramework: true, libraryDirectory: library, interfaceDirectory: ui);
        var start = Start.Matches(proposed)[0];
        var lines = removed.Concat(Numbered(proposed[start.Index..], NumberAt(proposed, start.Index), false)).ToArray();
        var bytes = source.Encode(original, proposed);
        if (bytes.Length > MaxEvarsBytes) throw new InvalidDataException("更新后的 EVARS 超过预览上限。");
        return new(path, original, bytes, lines);
    }

    public static PmlResourceSwitch PreviewRemoval(string evars)
    {
        var path = OpenEvars(evars);
        var original = FileContent.ReadBounded(path, MaxEvarsBytes);
        var source = LocalText.Decode(original);
        var (proposed, removed, _) = RemoveOldBlock(source.Content);
        if (removed.Length == 0) return new(path, original, original, []);
        RequireRoundTrip(original, source);
        return new(path, original, source.Encode(original, proposed), removed);
    }

    private static void RequireRoundTrip(byte[] original, LocalText source)
    {
        if (!source.Encode(original, source.Content).AsSpan().SequenceEqual(original))
            throw new InvalidDataException("无法按原编码保留 EVARS 字节，请先核对文件编码。");
    }

    public void RequireOriginal(bool writable = false)
    {
        var path = OpenEvars(Path);
        if (!FileContent.ReadBounded(path, MaxEvarsBytes).AsSpan().SequenceEqual(Original))
            throw new InvalidDataException($"EVARS 在预览后已经变化，请重新预览。\n{path}");
        if (writable)
        {
            using var probe = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.Read);
        }
    }

    public string Apply()
    {
        var path = OpenEvars(Path);
        var parent = LocalSource.OpenParent(path);
        var name = System.IO.Path.GetFileName(path);
        using var gate = File.Open(parent.FilePath(name + ".core30.lock"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        var token = Guid.NewGuid().ToString("N");
        var backup = parent.FilePath($"{name}.phayton-{token}.bak");
        var pending = parent.FilePath($".phayton-evars-{token}.tmp");
        try
        {
            using (var output = File.Open(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Write(Proposed);
                output.Flush(flushToDisk: true);
            }
            RequireOriginal();
            File.Replace(pending, path, backup);
            if (!FileContent.ReadBounded(path, MaxEvarsBytes).AsSpan().SequenceEqual(Proposed) ||
                !FileContent.ReadBounded(backup, MaxEvarsBytes).AsSpan().SequenceEqual(Original))
                throw new IOException($"EVARS 写入或备份核验失败。备份路径 {backup}");
            return backup;
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error) && File.Exists(backup))
        { throw new EvarsWriteException(backup, error); }
        finally { File.Delete(pending); }
    }

    public void Restore(string backup)
    {
        if (!FileContent.ReadBounded(backup, MaxEvarsBytes).AsSpan().SequenceEqual(Original))
            throw new IOException($"备份与预览不一致。\n{backup}");
        if (FileContent.ReadBounded(OpenEvars(Path), MaxEvarsBytes).AsSpan().SequenceEqual(Original)) return;
        (this with { Original = Proposed, Proposed = Original }).Apply();
    }

    private static string OpenEvars(string path)
    {
        path = LocalSource.RequireAbsolutePath(path);
        if (System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant() is not ("evars" or "evar") ||
            System.IO.Path.GetExtension(path).ToLowerInvariant() is not (".bat" or ".init"))
            throw new InvalidDataException("请选择本机的 EVARS 或 EVAR 文件。");
        return LocalSource.OpenParent(path).FilePath(System.IO.Path.GetFileName(path));
    }

    private static (string Text, EvarsLine[] Removed, string? Startup) RemoveOldBlock(string text)
    {
        var begins = Start.Matches(text);
        var ends = End.Matches(text);
        if (begins.Count == 0 && ends.Count == 0) return (text, [], null);
        if (begins.Count != 1 || ends.Count != 1 || begins[0].Index >= ends[0].Index)
            throw new InvalidDataException("EVARS 的旧托管块边界不完整，请先核对当前文件。");

        var first = begins[0];
        var last = ends[0];
        string? startup = null;
        using var reader = new StringReader(text[(first.Index + first.Length)..last.Index]);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var match = ManagedSetting.Match(line);
            if (!match.Success || match.Groups["value"].Value.IndexOfAny(['&', '|', '<', '>', '^']) >= 0)
                throw new InvalidDataException("EVARS 托管块包含自定义命令或无效赋值，请先核对当前文件。");
            if (match.Groups["name"].Value.Equals("YUZUHA_STARTUP_ACTIONS", StringComparison.OrdinalIgnoreCase))
                startup = match.Groups["value"].Value.Trim().ToLowerInvariant();
        }
        var next = last.Index + last.Length;
        if (next < text.Length && text[next] == '\n') next++;
        return (text.Remove(first.Index, next - first.Index), Numbered(text[first.Index..next], NumberAt(text, first.Index), true), startup);
    }

    private static Regex Marker(string arrow) => new($@"^[ \t]*@?rem[ \t]+{arrow}[ \t]+Yuzuha managed settings[ \t]*\r?$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static int NumberAt(string text, int index) => 1 + text.AsSpan(0, index).Count('\n');

    private static EvarsLine[] Numbered(string text, int number, bool removed) => text.TrimEnd('\r', '\n')
        .Split('\n').Select((line, offset) => new EvarsLine(number + offset, line.TrimEnd('\r'), removed)).ToArray();
}
