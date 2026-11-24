using System.Text.RegularExpressions;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.PmlTwin;

public sealed record PmlLibraryFolder(string Path, bool Official, string State);
public sealed record PmlCatalogParameter(string Name, string Type);
public sealed record PmlCatalogFunction(string Name, string Path, string Digest, string Declaration, string Summary,
    bool Official, string ReturnType, PmlCatalogParameter[] Parameters, bool CanRegister);
public sealed record PmlLibraryScan(PmlCatalogFunction[] Functions, int Encrypted, int Skipped, string[] Warnings);

public static partial class PmlTwinFeatures
{
    public static PmlLibraryFolder[] ReadLibraryFolders(string evars, string product, string installation, IEnumerable<string> supplied)
    {
        var environment = InstalledEvarsChange.Inspect(LocalSource.RequireAbsolutePath(evars), product);
        if (environment.AssignmentLine == 0)
            throw new InvalidDataException("EVARS 中没有可静态读取的 PMLLIB 设置。");
        var official = Path.Combine(LocalSource.RequireAbsolutePath(installation), "PMLLIB");
        var roots = environment.Roots.Select(root => new PmlLibraryFolder(root.Path, LocalSource.SamePath(root.Path, official), root.State));
        var extra = supplied.Select(path =>
        {
            var local = Path.TrimEndingDirectorySeparator(LocalSource.RequireAbsolutePath(path));
            return new PmlLibraryFolder(local, LocalSource.SamePath(local, official), Directory.Exists(local) ? "available" : "missing");
        });
        var installed = new PmlLibraryFolder(official, true, Directory.Exists(official) ? "available" : "missing");
        return new[] { installed }.Concat(roots).Concat(extra).DistinctBy(root => root.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static PmlLibraryScan ScanLibraryFunctions(IReadOnlyList<PmlLibraryFolder> folders)
    {
        var rows = new List<PmlCatalogFunction>();
        var warnings = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var encrypted = 0;
        var skipped = 0;
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (var folder in folders)
        {
            if (folder.State != "available")
            {
                warnings.Add(folder.Path + (folder.State == "unresolved" ? " · EVARS 变量未解析" : " · 目录不可用"));
                continue;
            }
            try
            {
                var scope = new FeatureContext(folder.Path);
                foreach (var file in Directory.EnumerateFiles(scope.Root, "*.pmlfnc", options))
                {
                    if (!seen.Add(file)) continue;
                    try
                    {
                        var bytes = ReadBounded(scope.FilePath(Path.GetRelativePath(scope.Root, file)), FunctionFileLimit);
                        if (PmlSourceHeader.ReadAlgorithm(bytes) is not ("" or "000"))
                        {
                            encrypted++;
                            continue;
                        }
                        var source = LocalText.Decode(bytes).Content;
                        var declarations = Declaration.Matches(source).Cast<Match>().ToArray();
                        if (declarations.Length == 0)
                        {
                            skipped++;
                            continue;
                        }
                        var digest = Sha256(bytes);
                        var summary = FunctionSummary(source);
                        foreach (var group in declarations.GroupBy(match => match.Groups[1].Value, StringComparer.OrdinalIgnoreCase))
                            if (group.Take(2).Count() == 1)
                                rows.Add(DescribeCatalogFunction(file, digest, summary, folder.Official, group.First()));
                            else skipped++;
                    }
                    catch (Exception error) when (CoreRunner.IsOperationalError(error) || error is RegexMatchTimeoutException) { skipped++; }
                }
            }
            catch (Exception error) when (CoreRunner.IsOperationalError(error)) { warnings.Add(folder.Path + " · " + error.Message); }
        }
        return new(rows.ToArray(), encrypted, skipped, warnings.ToArray());
    }

    private static PmlCatalogFunction DescribeCatalogFunction(string path, string digest, string summary, bool official, Match match)
    {
        var name = "!!" + match.Groups[1].Value;
        var args = match.Groups[2].Value.Trim();
        var returnType = match.Groups[3].Value.ToUpperInvariant();
        var signature = $"{name}({args})" + (returnType.Length == 0 ? "" : " is " + returnType);
        var parts = args.Length == 0 ? [] : args.Split(',').Select(text => Parameter.Match(text)).ToArray();
        var supported = name.Length <= 130 && parts.All(part => part.Success) &&
            parts.Select(part => part.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() == parts.Length;
        var parameters = supported ? parts.Select(part => new PmlCatalogParameter(part.Groups[1].Value,
            part.Groups[2].Value.ToUpperInvariant())).ToArray() : [];
        return new(name, path, digest, signature, summary, official, returnType, parameters,
            supported && returnType is "" or "UNDEFINED" or "VOID" or "ARRAY" or "STRING" or "REAL" or "BOOLEAN");
    }
}
