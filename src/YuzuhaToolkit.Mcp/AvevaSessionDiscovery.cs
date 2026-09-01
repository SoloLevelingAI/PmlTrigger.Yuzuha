using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace YuzuhaToolkit.Mcp;

public sealed class AvevaSessionCandidate
{
    public int ProcessId { get; set; }
    public long ProcessStartTimeUtcTicks { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string PipeName { get; set; } = string.Empty;
    public bool PipeDetected { get; set; }
}

public sealed class AvevaSessionListResponse
{
    public int Count { get; set; }
    public List<AvevaSessionCandidate> Sessions { get; set; } = new();
    public string? Error { get; set; }
}

public sealed class AvevaSessionDiscovery
{
    private const string PipePrefix = "yuzuha.pml.command.v1.pid-";

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
                if (!IsRecognizedAvevaTitle(title))
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

    private static bool IsRecognizedAvevaTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        // Window titles are used only for candidate discovery. Selection still
        // requires the PID-bound Yuzuha pipe and verifies the process ID,
        // process start time, and host identity, so discovery can remain broad
        // enough to cover AVEVA products such as Hull & Outfitting without a
        // product-by-product title allowlist.
        return title.Contains("AVEVA", StringComparison.OrdinalIgnoreCase);
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

    private sealed record WindowIdentity(int ProcessId, string Title);

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
