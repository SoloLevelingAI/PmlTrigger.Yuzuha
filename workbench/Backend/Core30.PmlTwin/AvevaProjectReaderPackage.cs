namespace Core30.PmlTwin;

/// <summary>Locates the separately distributed AVEVA project reader for this host.</summary>
public static class AvevaProjectReaderPackage
{
    internal static string Executable => Path.Combine(AppContext.BaseDirectory, "Core30.AvevaProjectReader.exe");
    public static bool IsInstalled => File.Exists(Executable);
}
