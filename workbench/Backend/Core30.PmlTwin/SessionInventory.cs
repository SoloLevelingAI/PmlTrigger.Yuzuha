using System.ComponentModel;
using System.Diagnostics;

namespace Core30.PmlTwin;

internal sealed record SessionCandidate(int ProcessId, long StartTicks, string Product);

internal interface ISessionInventory
{
    IReadOnlyList<SessionCandidate> Find();
}

internal sealed class WindowsSessionInventory : ISessionInventory
{
    private static readonly (string Marker, string Product)[] ProductNames =
        [("Everything3D", "E3D"), ("AVEVA E3D", "E3D"), ("Marine", "Marine"), ("PDMS", "PDMS")];

    public IReadOnlyList<SessionCandidate> Find()
    {
        RequireWindows();
        var found = new List<SessionCandidate>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                var candidate = ReadVisibleCandidate(process);
                if (candidate is not null)
                    found.Add(candidate);
            }
        }
        return found.OrderBy(candidate => candidate.ProcessId).ToArray();
    }

    private static SessionCandidate? ReadVisibleCandidate(Process process)
    {
        try
        {
            return InspectWindow(process);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or NotSupportedException or IOException)
        {
            return null;
        }
    }

    private static SessionCandidate? InspectWindow(Process process)
    {
        var product = VisibleProduct(process);
        if (product is null)
            return null;

        var pid = process.Id;
        var startTicks = process.StartTime.ToUniversalTime().Ticks;
        return new SessionCandidate(pid, startTicks, product);
    }

    private static string? VisibleProduct(Process process) =>
        process.MainWindowHandle == IntPtr.Zero ? null : MatchProduct(process.MainModule?.FileVersionInfo);

    private static string? MatchProduct(FileVersionInfo? module)
    {
        if (module is null)
            return null;
        var metadata = $"{module.ProductName} {module.FileDescription} {module.FileName}";
        if (!metadata.Contains("AVEVA", StringComparison.OrdinalIgnoreCase)
            && module.CompanyName?.Contains("AVEVA", StringComparison.OrdinalIgnoreCase) != true)
            return null;
        return ProductNames.FirstOrDefault(product => metadata.Contains(product.Marker, StringComparison.OrdinalIgnoreCase)).Product;
    }

    private static void RequireWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new InvalidOperationException("Windows window inventory is required for P02 discovery.");
    }
}
