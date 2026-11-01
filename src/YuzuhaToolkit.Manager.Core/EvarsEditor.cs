using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using YuzuhaToolkit.Deployment;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>冻结环境文件的原始字节、编码和托管段；预览不写文件。</summary>
public sealed record EvarsEditPlan(string File, string RuntimeRoot, string Profile, string Framework,
    int CodePage, string BeforeHash, string AfterHash, string OriginalBase64, string UpdatedBase64, string? StartupActions = null);

/// <summary>实际写入的前后身份与原文件备份。</summary>
public sealed record EvarsEditReceipt(string Id, string File, string? BackupFile, string BeforeHash, string AfterHash);

/// <summary>环境写入失败时保留可安全核验的恢复身份。</summary>
public sealed class EvarsApplyException(string message, EvarsEditReceipt? receipt, Exception inner) : IOException(message, inner)
{
    /// <summary>替换可能完成时的条件恢复回执，恢复仍须核对当前内容。</summary>
    public EvarsEditReceipt? Receipt { get; } = receipt;
}

/// <summary>独立于 MCP 注册的环境编辑器；只更新显式选择的批处理风格 INIT/BAT。</summary>
public static class EvarsEditor
{
    private const string Start = "rem >>> Yuzuha managed settings";
    private const string End = "rem <<< Yuzuha managed settings";

    /// <summary>优先目标安装的 INIT；AM/PDMS 确认没有 INIT 时才回退 BAT。</summary>
    public static string SelectFile(string installationRoot, string product, string? selectedFile = null)
    {
        installationRoot = Path.GetFullPath(installationRoot); PathRules.NoLinks(installationRoot);
        product = product.ToUpperInvariant();
        if (product is not ("E3D" or "AM" or "PDMS")) throw new ArgumentException("产品必须是 E3D、AM 或 PDMS。");
        bool Exists(string file)
        {
            try { return (System.IO.File.GetAttributes(file) & FileAttributes.Directory) == 0; }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            // Access denied is not an absent INIT and must never select another file.
        }
        var init = new[] { "evars.init", "evar.init" }.Select(n => Path.Combine(installationRoot, n)).Where(Exists).ToArray();
        var choices = init.Length > 0 ? init : product == "E3D" ? [] :
            new[] { "evars.bat", "evar.bat" }.Select(n => Path.Combine(installationRoot, n)).Where(Exists).ToArray();
        if (selectedFile is not null)
        {
            selectedFile = Path.GetFullPath(selectedFile);
            if (!choices.Contains(selectedFile, StringComparer.OrdinalIgnoreCase))
                throw new IOException("所选文件不是该安装适用的 INIT/BAT；请核对实际启动链。");
            return selectedFile;
        }
        return choices.Length == 1 ? choices[0] : throw new IOException(choices.Length == 0
            ? "目标安装缺少适用的环境文件；E3D 必须有有效 INIT。" : "存在多个候选环境文件，请按实际启动链明确选择。");
    }

    /// <summary>生成保留编码与非托管正文的差异；无 BOM 自动模式逐字节保留，非 ASCII 路径须明确编码。</summary>
    public static EvarsEditPlan Preview(string file, string runtimeRoot, string profile, string framework, int? codePage = null)
    {
        file = Path.GetFullPath(file); runtimeRoot = Path.GetFullPath(runtimeRoot);
        PathRules.NoLinks(file); PathRules.NoLinks(runtimeRoot);
        if (!new[] { ".init", ".bat" }.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase) || System.IO.File.Exists(runtimeRoot))
            throw new ArgumentException("请选择批处理风格的 INIT/BAT 和运行目录。");
        if (runtimeRoot.IndexOfAny(['\r', '\n', '"', '%', '!', '&', '|', '<', '>', '^', '(', ')']) >= 0 ||
            !Regex.IsMatch(profile, @"^[A-Za-z0-9][A-Za-z0-9.-]{0,63}$") || framework is not ("net35" or "net48"))
            throw new ArgumentException("运行路径、profile 或 framework 不能安全表示为批处理环境设置。");
        var info = new FileInfo(file);
        if (info.Length > 2 * 1024 * 1024) throw new IOException("环境文件超过 2 MiB，拒绝自动编辑。");
        var bytes = System.IO.File.ReadAllBytes(file);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bom = bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 :
            bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe }) || bytes.AsSpan().StartsWith(new byte[] { 0xfe, 0xff }) ? 2 : 0;
        var cp = bom == 3 ? 65001 : bom == 2 ? bytes[0] == 0xff ? 1200 : 1201 :
            codePage ?? 28591;
        if (bom == 0 && codePage is null && runtimeRoot.Any(c => c > 127))
            throw new ArgumentException("无 BOM 文件与非 ASCII 运行路径需要明确选择代码页。");
        var encoding = Encoding.GetEncoding(cp, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var text = encoding.GetString(bytes, bom, bytes.Length - bom);
        if (!bytes.AsSpan(0, bom).ToArray().Concat(encoding.GetBytes(text)).SequenceEqual(bytes))
            throw new ArgumentException("编码不能逐字节往返，请明确选择正确代码页。");
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var starts = Regex.Matches(text, @"(?im)^[ \t]*" + Regex.Escape(Start) + @"[ \t]*\r?$").Count;
        var ends = Regex.Matches(text, @"(?im)^[ \t]*" + Regex.Escape(End) + @"[ \t]*\r?$").Count;
        var blocks = Regex.Matches(text, @"(?ims)^[ \t]*" + Regex.Escape(Start) + @"[ \t]*\r?\n.*?^[ \t]*" + Regex.Escape(End) + @"[ \t]*(?:\r?\n|$)");
        if (starts != ends || blocks.Count != starts) throw new IOException("Yuzuha 托管段标记不完整，未修改原文。");
        var policies = blocks.Cast<Match>().SelectMany(block => Regex.Matches(block.Value,
            @"(?im)^[ \t]*@?set\s+""?YUZUHA_STARTUP_ACTIONS=([^\r\n]*)\r?$").Cast<Match>())
            .Select(m => m.Groups[1].Value.Trim().Trim('"').ToLowerInvariant()).Distinct().ToArray();
        if (policies.Length > 1 || policies.Any(p => p is not ("none" or "rehash-all")))
            throw new IOException("原托管启动策略存在冲突或未知值，未修改。");
        var startup = policies.FirstOrDefault();
        var body = Regex.Replace(text, @"(?ims)^[ \t]*" + Regex.Escape(Start) + @"[ \t]*\r?\n.*?^[ \t]*" + Regex.Escape(End) + @"[ \t]*(?:\r?\n|$)", "");
        var updatedText = EnvironmentTextEditor.Edit(body, runtimeRoot, profile, framework == "net35", newline,
            out _, startup, includeFramework: true);
        var payload = encoding.GetBytes(updatedText);
        var updated = bytes.AsSpan(0, bom).ToArray().Concat(payload).ToArray();
        return new(file, runtimeRoot, profile, framework, cp, Hash(bytes), Hash(updated),
            Convert.ToBase64String(bytes), Convert.ToBase64String(updated), startup);
    }

    /// <summary>复核整个计划后备份并原子替换；失败保留可定位的备份和阶段。</summary>
    public static EvarsEditReceipt Apply(EvarsEditPlan plan)
    {
        var current = Preview(plan.File, plan.RuntimeRoot, plan.Profile, plan.Framework, plan.CodePage);
        if (current != plan) throw new IOException("环境文件或计划在预览后变化，请重新预览。");
        if (plan.BeforeHash == plan.AfterHash) return new("unchanged", plan.File, null, plan.BeforeHash, plan.AfterHash);
        var id = Guid.NewGuid().ToString("N");
        var backup = plan.File + ".yuzuha-" + id + ".bak";
        var temporary = plan.File + ".yuzuha-" + id + ".tmp";
        var updated = Convert.FromBase64String(plan.UpdatedBase64);
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(updated); output.Flush(true); }
            if (Hash(System.IO.File.ReadAllBytes(plan.File)) != plan.BeforeHash)
                throw new IOException("环境文件被其他程序修改，未覆盖。");
            System.IO.File.Replace(temporary, plan.File, backup);
            if (Hash(System.IO.File.ReadAllBytes(plan.File)) != plan.AfterHash)
                throw new IOException("环境文件写后核验失败；请使用备份检查恢复：" + backup);
            return new(id, plan.File, backup, plan.BeforeHash, plan.AfterHash);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { throw new EvarsApplyException("EVARS apply: " + (string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message) + " Backup=" + backup,
            System.IO.File.Exists(backup) ? new(id, plan.File, backup, plan.BeforeHash, plan.AfterHash) : null, error); }
        finally { if (System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary); }
    }

    /// <summary>仅恢复本次写入内容；原始指纹已恢复时确认成功，其他外部修改和备份损坏拒绝覆盖。</summary>
    public static void Restore(EvarsEditReceipt receipt)
    {
        PathRules.NoLinks(receipt.File);
        var current = Hash(System.IO.File.ReadAllBytes(receipt.File));
        // A lost reply or partial multi-file recovery can leave this file already
        // restored. Confirm the exact original bytes without replaying a write.
        if (current == receipt.BeforeHash) return;
        if (receipt.BackupFile is null) throw new IOException("缺少原文件备份。");
        PathRules.NoLinks(receipt.BackupFile);
        if (current != receipt.AfterHash ||
            Hash(System.IO.File.ReadAllBytes(receipt.BackupFile)) != receipt.BeforeHash)
            throw new IOException("环境文件或备份已变化，保留现场并拒绝恢复覆盖。");
        var temporary = receipt.File + ".restore-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            System.IO.File.Copy(receipt.BackupFile, temporary, false);
            if (Hash(System.IO.File.ReadAllBytes(temporary)) != receipt.BeforeHash)
                throw new IOException("备份在读取期间变化，未覆盖环境文件。");
            if (Hash(System.IO.File.ReadAllBytes(receipt.File)) != receipt.AfterHash)
                throw new IOException("恢复前环境文件已变化。");
            System.IO.File.Replace(temporary, receipt.File, null);
            if (Hash(System.IO.File.ReadAllBytes(receipt.File)) != receipt.BeforeHash) throw new IOException("恢复后的内容不匹配原文件。");
        }
        finally { if (System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary); }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
