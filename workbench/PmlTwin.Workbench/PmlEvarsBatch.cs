using System.IO;
using Core30.Backend;
using Core30.Core;
using Core30.PmlTwin;

namespace PmlTwin.Workbench;

internal sealed record PmlEvarsTarget(string Version, string File, string Host, string Status, PmlResourceSwitch? Edit = null)
{
    public override string ToString() => $"{Version} · {File}";
}

internal sealed record PmlEvarsBackup(string File, string Backup);

internal sealed class PmlEvarsBatchException(string message, bool restored, Exception inner) : IOException(message, inner)
{
    public bool Restored { get; } = restored;
}

internal sealed class PmlEvarsBatch(PmlEvarsTarget[] targets)
{
    public PmlEvarsTarget[] Targets { get; } = targets;
    private IEnumerable<PmlResourceSwitch> Edits => Targets.Select(target => target.Edit).OfType<PmlResourceSwitch>()
        .DistinctBy(edit => edit.Path, StringComparer.OrdinalIgnoreCase);
    public int ChangeCount => Edits.Count(edit => edit.Changed);
    public int NeedsReview => Targets.Count(target => target.Edit is null);

    public static PmlEvarsBatch Preview(string program, string library, string ui, IEnumerable<(LaunchInstallation Install, string File)> installations)
    {
        var rows = installations.Select(target => PreviewTarget(program, library, ui, target.Install, target.File)).ToArray();
        var conflicts = rows.Where(row => row.Edit is not null).GroupBy(row => row.Edit!.Path, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(row => row.Host).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new(rows.Select(row => row.Edit is not null && conflicts.Contains(row.Edit.Path)
            ? row with { Edit = null, Status = "同一 EVARS 对应不同 Host，请核对启动文件。" } : row).ToArray());
    }

    private static PmlEvarsTarget PreviewTarget(string program, string library, string ui, LaunchInstallation installation, string file)
    {
        var host = PmlHostConfiguration.AtProgramDirectory(program, installation.Product, installation.Release);
        if (host.DllPath.Length == 0) return new(installation.Name, file, host.Profile, "未找到该版本的 Host DLL。");
        if (file.Length == 0) return new(installation.Name, file, host.Profile, "未找到实际启动用的 EVARS。");
        try
        {
            var edit = PmlResourceSwitch.Preview(file, program, library, ui, installation.Product, installation.Release);
            return new(installation.Name, edit.Path, host.Profile, edit.Changed ? "待写入" : "已接入", edit);
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error))
        { return new(installation.Name, file, host.Profile, error.Message); }
    }

    public void RequireWritable()
    {
        foreach (var edit in Edits)
        {
            try { edit.RequireOriginal(writable: edit.Changed); }
            catch (UnauthorizedAccessException error)
            { throw new UnauthorizedAccessException($"需要 EVARS 所在目录的写入权限。\n{edit.Path}", error); }
        }
    }

    public PmlEvarsBackup[] Apply()
    {
        var written = new List<(PmlResourceSwitch Edit, string Backup)>();
        try
        {
            foreach (var edit in Edits)
            {
                if (!edit.Changed) { edit.RequireOriginal(); continue; }
                try { written.Add((edit, edit.Apply())); }
                catch (EvarsWriteException error) { written.Add((edit, error.Backup)); throw; }
            }
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error))
        {
            var conflicts = new List<string>();
            foreach (var entry in written.AsEnumerable().Reverse())
            {
                try { entry.Edit.Restore(entry.Backup); }
                catch (Exception restore) when (CoreRunner.IsOperationalError(restore))
                { conflicts.Add($"{entry.Edit.Path}\n备份 {entry.Backup}\n{restore.Message}"); }
            }
            var message = conflicts.Count == 0 ? "已恢复本次写入的 EVARS。" : $"需核对回滚冲突。\n{string.Join("\n", conflicts)}";
            throw new PmlEvarsBatchException($"{error.Message}\n{message}", conflicts.Count == 0, error);
        }
        return written.Select(item => new PmlEvarsBackup(item.Edit.Path, item.Backup)).ToArray();
    }
}
