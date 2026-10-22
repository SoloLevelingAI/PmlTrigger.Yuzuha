using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace YuzuhaToolkit.Mcp;

/// <summary>从可见窗口识别出的 AVEVA 进程、启动时间、项目和专用管道候选。</summary>
public sealed class AvevaSessionCandidate
{
    /// <summary>目标或发送方操作系统进程编号；不能代替启动时间核验。</summary>
    public int ProcessId { get; set; }
    /// <summary>进程启动 UTC ticks；与 PID 联合识别目标，防止 PID 重用。</summary>
    public long ProcessStartTimeUtcTicks { get; set; }
    /// <summary>从操作系统读取的进程名称。</summary>
    public string ProcessName { get; set; } = string.Empty;
    /// <summary>可获取时的目标可执行文件路径。</summary>
    public string ExecutablePath { get; set; } = string.Empty;
    /// <summary>被识别 AVEVA 可见窗口的原始标题。</summary>
    public string WindowTitle { get; set; } = string.Empty;
    /// <summary>从窗口或进程识别的 AVEVA 产品名称。</summary>
    public string Product { get; set; } = string.Empty;
    /// <summary>可获取时从会话信息识别的工程项目名称。</summary>
    public string Project { get; set; } = string.Empty;
    /// <summary>绑定该目标 PID 的专用 Named Pipe 名称。</summary>
    public string PipeName { get; set; } = string.Empty;
    /// <summary>发现阶段是否观察到预期的 PID 专用管道。</summary>
    public bool PipeDetected { get; set; }
}

/// <summary>会话发现结果；列出候选但不自动选择进程。</summary>
public sealed class AvevaSessionListResponse
{
    /// <summary>当前返回集合的条目数量。</summary>
    public int Count { get; set; }
    /// <summary>只读发现的会话候选，尚未自动选定任何一个。</summary>
    public List<AvevaSessionCandidate> Sessions { get; set; } = new();
    /// <summary>当前查询、连接或处理失败的可读原因。</summary>
    public string? Error { get; set; }
}

/// <summary>可注入的只读会话发现与目标身份校验边界。</summary>
public interface IAvevaSessionDiscovery
{
    /// <summary>列出可见的 AVEVA 会话和专用管道，不连接或自动选择。</summary>
    AvevaSessionListResponse Discover();
    /// <summary>重新验证明确选择的 PID，返回当前窗口与进程启动身份。</summary>
    AvevaSessionCandidate RequireCandidate(int processId);
}

/// <summary>从可见工程窗口、进程启动身份和专用管道发现候选。</summary>
public sealed class AvevaSessionDiscovery : IAvevaSessionDiscovery
{
    private const string PipePrefix = "yuzuha.pml.command.v1.pid-";

    /// <summary>读取可见 AVEVA 窗口、进程启动时间与专用管道候选，不自动连接会话。</summary>
    public AvevaSessionListResponse Discover()
    {
        if (!OperatingSystem.IsWindows())
            return new AvevaSessionListResponse
            {
                Error = "AVEVA window discovery is supported only on Windows."
            };

        try
        {
            var pipeNames = ReadPipeNames();
            var sessions = EnumerateAvevaWindows()
                .GroupBy(window => window.ProcessId)
                .Select(group => CreateCandidate(
                    group.Key,
                    group.OrderByDescending(window => ScoreTitle(window.Title))
                        .First().Title,
                    pipeNames))
                .Where(candidate => candidate != null)
                .Cast<AvevaSessionCandidate>()
                .OrderBy(candidate => candidate.Product,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Project,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.ProcessId)
                .ToList();

            return new AvevaSessionListResponse
            {
                Count = sessions.Count,
                Sessions = sessions
            };
        }
        catch (Exception exception)
        {
            return new AvevaSessionListResponse
            {
                Error = "AVEVA_SESSION_DISCOVERY_FAILED: " + exception.Message
            };
        }
    }

    /// <summary>重新发现并要求指定 PID 仍对应有效 AVEVA 候选，拒绝猜测或过期身份。</summary>
    /// <param name="processId">从会话发现结果取得的准确目标 PID，不能猜测。</param>
    public AvevaSessionCandidate RequireCandidate(int processId)
    {
        if (processId <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(processId),
                "The AVEVA process ID must be positive.");

        var response = Discover();
        if (!string.IsNullOrEmpty(response.Error))
            throw new InvalidOperationException(response.Error);

        var candidate = response.Sessions.SingleOrDefault(
            session => session.ProcessId == processId);
        if (candidate == null)
            throw new InvalidOperationException(
                "AVEVA_SESSION_NOT_DISCOVERED: PID " + processId +
                " does not own a recognized visible AVEVA window. Call " +
                "list_aveva_sessions again and select one returned PID.");

        return candidate;
    }

    private static List<WindowIdentity> EnumerateAvevaWindows()
    {
        List<WindowIdentity> windows = new();
        _ = EnumWindows(
            delegate(IntPtr windowHandle, IntPtr state)
            {
                if (!IsWindowVisible(windowHandle))
                    return true;

                StringBuilder titleBuilder = new(2048);
                _ = GetWindowText(
                    windowHandle,
                    titleBuilder,
                    titleBuilder.Capacity);
                var title = titleBuilder.ToString().Trim();
                if (string.IsNullOrWhiteSpace(title))
                    return true;

                _ = GetWindowThreadProcessId(
                    windowHandle,
                    out var processId);
                if (processId > 0)
                    windows.Add(new WindowIdentity((int)processId, title));

                return true;
            },
            IntPtr.Zero);
        return windows;
    }

    private static AvevaSessionCandidate? CreateCandidate(
        int processId,
        string windowTitle,
        HashSet<string> pipeNames)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            var executablePath = TryReadExecutablePath(process);
            if (!IsEngineeringWindow(windowTitle, process.ProcessName, executablePath)) return null;
            var pipeName = PipePrefix + processId.ToString(
                CultureInfo.InvariantCulture);
            return new AvevaSessionCandidate
            {
                ProcessId = processId,
                ProcessStartTimeUtcTicks =
                    process.StartTime.ToUniversalTime().Ticks,
                ProcessName = process.ProcessName,
                ExecutablePath = executablePath,
                WindowTitle = windowTitle,
                Product = DetectProduct(windowTitle, executablePath),
                Project = DetectProject(windowTitle),
                PipeName = pipeName,
                PipeDetected = pipeNames.Contains(pipeName)
            };
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string TryReadExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static HashSet<string> ReadPipeNames()
    {
        HashSet<string> result = new(StringComparer.Ordinal);
        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(
                         @"\\.\pipe\"))
            {
                var separator = entry.LastIndexOf('\\');
                result.Add(separator >= 0 ? entry[(separator + 1)..] : entry);
            }
        }
        catch
        {
            // Pipe detection is informational. Selection still uses a bounded
            // RPC connect timeout and host identity verification.
        }

        return result;
    }

    /// <summary>结合模块进程和产品路径识别工程窗口，避免把标题含 AVEVA 的启动台当作会话。</summary>
    /// <param name="title">当前可见窗口标题。</param>
    /// <param name="processName">操作系统实际进程名。</param>
    /// <param name="executablePath">可读取时的实际可执行文件路径。</param>
    public static bool IsEngineeringWindow(string title, string processName, string executablePath)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        var name = Path.GetFileNameWithoutExtension(processName).ToLowerInvariant();
        // Launchers, consoles, editors and documentation windows are not engineering sessions.
        if (name is not ("des" or "design" or "paragon" or "dra" or "draft" or "adm" or "admin" or "isodraft" or "specon" or "propcon" or "lexicon" or "spooler" or "hull" or "outfitting")) return false;
        var installedProduct = executablePath.Contains("Everything3D", StringComparison.OrdinalIgnoreCase)
            || executablePath.Contains("Marine", StringComparison.OrdinalIgnoreCase)
            || executablePath.Contains("PDMS", StringComparison.OrdinalIgnoreCase);
        return installedProduct || Regex.IsMatch(title, @"\b(AVEVA|PDMS|Marine)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static int ScoreTitle(string title)
    {
        var score = title.Contains("AVEVA", StringComparison.OrdinalIgnoreCase)
            ? 100
            : 0;
        if (title.Contains("Project", StringComparison.OrdinalIgnoreCase))
            score += 10;
        if (title.Contains('|'))
            score += 5;
        return score;
    }

    private static string DetectProduct(string title, string executablePath)
    {
        if (title.Contains("Everything3D", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("AVEVA E3D", StringComparison.OrdinalIgnoreCase))
            return "E3D";
        if (title.Contains("AVEVA PDMS", StringComparison.OrdinalIgnoreCase))
            return "PDMS";
        if (title.Contains("AVEVA Marine", StringComparison.OrdinalIgnoreCase))
            return "AM";
        if (executablePath.Contains("Everything3D",
                StringComparison.OrdinalIgnoreCase))
            return "E3D";
        if (executablePath.Contains("Marine", StringComparison.OrdinalIgnoreCase)) return "AM";
        if (executablePath.Contains("PDMS", StringComparison.OrdinalIgnoreCase)) return "PDMS";
        return "AVEVA";
    }

    private static string DetectProject(string title)
    {
        var pdmsMatch = Regex.Match(
            title,
            @"Project\s*-\s*([^\)]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (pdmsMatch.Success)
            return pdmsMatch.Groups[1].Value.Trim();

        var separator = title.IndexOf('|');
        return separator > 0 ? title[..separator].Trim() : string.Empty;
    }

    /// <summary>从一个可见窗口提取的产品、项目与标题身份。</summary>
    /// <param name="ProcessId">从会话发现结果取得的准确目标 PID，不能猜测。</param>
    /// <param name="Title">资料或经验的可读标题。</param>
    private sealed record WindowIdentity(int ProcessId, string Title);

    /// <summary>Win32 窗口枚举回调；返回是否继续枚举。</summary>
    /// <param name="windowHandle">用于读取窗口边界或内容的 Win32 句柄。</param>
    /// <param name="state">由调用方明确指定的状态或原生枚举上下文。</param>
    private delegate bool EnumWindowsCallback(IntPtr windowHandle, IntPtr state);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(
        EnumWindowsCallback callback,
        IntPtr state);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(
        IntPtr windowHandle,
        StringBuilder text,
        int maximumCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr windowHandle,
        out uint processId);
}
