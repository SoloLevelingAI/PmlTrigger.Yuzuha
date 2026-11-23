namespace Core30.PmlTwin;

internal enum AvevaProduct { E3D, AM, PDMS }

internal sealed record EnvironmentScanOptions(string Mode, int MaxDepth, int MaxDirectories, string[] ExtraRoots);
internal sealed record ScanRoot(string Path, string Source, AvevaProduct? Product = null, string? Release = null);
internal sealed record ScanIssue(string Source, string Path, string Reason);
internal sealed record AvevaProcess(int ProcessId, long StartTicks, AvevaProduct Product, string Executable);
internal sealed record AvevaInstallation(AvevaProduct Product, string Release, string BinaryVersion, string Directory,
    string EntryPoint, bool LaunchFilesFound, string Vendor, string[] Evidence, string Source);
internal sealed record RootCoverage(string Path, string Source, string State, int Visited);
internal sealed record ScanCoverage(int Visited, int Unreadable, int ReparseSkipped, int DepthLimited, bool BudgetReached,
    int RegistryViews, RootCoverage[] Roots, ScanIssue[] Issues);
internal sealed record AvevaEnvironmentSnapshot(AvevaInstallation[] Installations, AvevaProcess[] Processes, ScanCoverage Coverage);
internal sealed record EnvironmentClues(ScanRoot[] Roots, AvevaProcess[] Processes, int RegistryViews, ScanIssue[] Issues);

internal interface IAvevaEnvironmentInventory
{
    EnvironmentClues Collect(string mode);
}
