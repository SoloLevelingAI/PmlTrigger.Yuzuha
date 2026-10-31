#pragma warning disable CS1591 // Retained prototype types; provenance documented in manager-launch.md.
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace YuzuhaToolkit.Manager.LaunchDesk;

// Read configuration as evidence, never execute it. This is deliberately not a cmd interpreter.
/// <summary>在预算内解析安装目录的 INIT/BAT 配置与模块文件，收集项目和模块线索而不执行脚本。</summary>
public sealed class FindAvevaProjectsAndModules(IFileSystem files)
{
    /// <summary>使用独立扫描状态读取指定安装目录，按提供的预算返回候选与诊断并响应取消。</summary>
    public ProjectModuleReport Execute(string installation, ProjectModuleOptions? options = null,
        CancellationToken ct = default) => new Scan(files, installation, options ?? new(), ct).Execute();

    /// <summary>保存一次发现所需的变量、已访问路径、结果集合和时间预算，避免不同扫描共享状态。</summary>
    private sealed class Scan(IFileSystem files, string installation, ProjectModuleOptions options, CancellationToken ct)
    {
        private static readonly RegexOptions Flags = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        private static Regex Pattern(string expression) => new(expression, Flags, TimeSpan.FromMilliseconds(100));
        private static readonly Regex Variable = Pattern("%([^%]+)%");
        private static readonly Regex Code = Pattern("^[A-Z0-9_]{2,16}$");
        private static readonly Regex ProjectVariable = Pattern("^([A-Z0-9_]{2,16})000$");
        private static readonly Regex ContentVariable = Pattern("^[A-Z0-9_]{2,16}(PIC|MAC|ISO|DWG|DIA|STE|TPL|PSI|GCD|DFLTS|DATA|INFO|REPORTS|MAR|DRG)$");
        private static readonly Regex Conditional = Pattern("""^if\s+(?<not>not\s+)?exist\s+(?:"(?<quoted>[^"]+)"|(?<bare>\S+))\s+(?<command>.+)$""");
        private static readonly Regex ModuleDispatch = Pattern("%(?:moduleArg|module|pdms_module)%\"\\s*==\\s*\"(?<name>[A-Z][A-Z0-9_]{1,31})\"");
        private static readonly Regex Tokens = Pattern("\"[^\"]*\"|\\S+");
        private readonly Dictionary<string, string> variables = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ProjectHint> projects = [];
        private readonly List<ModuleHint> modules = [];
        private readonly List<ProjectRootHint> roots = [];
        private readonly HashSet<string> active = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> readFiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> visitedDirectories = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> contentDirectories = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<DiscoveryDiagnostic> diagnostics = [];
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private int reads;
        private bool stopped;

        /// <summary>校验预算后先读取标准配置入口，再扫描项目目录及模块可执行文件，返回去重排序结果。</summary>
        internal ProjectModuleReport Execute()
        {
            if (options.MaximumFiles < 1 || options.MaximumDepth < 0 || options.MaximumDirectories < 1 ||
                options.MaximumFileBytes is < 1 or > 4194304 || options.TimeoutSeconds < 1)
                throw new ArgumentOutOfRangeException(nameof(options));

            // Fallback batch settings first; native initialization files can override them.
            foreach (var name in new[] { "evars.bat", "evars.init", "launch.init", "launch.bat", "pdms.bat" })
                Read(Path.Combine(installation, name), [], 0);
            WalkProjectDirectories();
            FindModuleExecutables();
            return new(projects.Distinct().OrderBy(value => value.Code).ThenBy(value => value.Directory).ToArray(),
                modules.Distinct().OrderBy(value => value.Name).ToArray(), roots.Distinct().ToArray(),
                readFiles.OrderBy(value => value).ToArray(), diagnostics.ToArray());
        }

        private bool CanContinue()
        {
            ct.ThrowIfCancellationRequested();
            if (!stopped && clock.Elapsed.TotalSeconds >= options.TimeoutSeconds)
            {
                Note("EnvironmentTimeout", installation, "Project/module discovery reached its time limit.");
                stopped = true;
            }
            return !stopped;
        }

        private void Read(string path, string[] arguments, int depth)
        {
            if (!CanContinue() || !files.FileExists(path)) return;
            path = Path.GetFullPath(path);
            if (depth > options.MaximumDepth)
            {
                Note("IncludeDepth", path, "Configuration include depth reached.");
                return;
            }
            if (!active.Add(path)) { Note("IncludeCycle", path, "Configuration include cycle skipped."); return; }
            try
            {
                if (files.IsReparsePoint(path)) { Note("LinkSkipped", path, "Linked configuration skipped."); return; }
                if (++reads > options.MaximumFiles)
                {
                    Note("EnvironmentFileLimit", path, "Configuration read limit reached.");
                    stopped = true;
                    return;
                }
                var text = files.ReadText(path, options.MaximumFileBytes);
                readFiles.Add(path);
                using var reader = new StringReader(text);
                var number = 0;
                while (reader.ReadLine() is { } line && CanContinue())
                {
                    number++;
                    try { Parse(line.Trim().TrimStart('@').TrimStart(), path, number, arguments, depth); }
                    catch (ArgumentException)
                    { Note("InvalidConfigurationValue", path, $"Invalid path or value at line {number} skipped."); }
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.DecoderFallbackException)
            {
                Note("ConfigurationReadFailed", path, "Cannot read bounded UTF-8/BOM text; check access, encoding and byte limit.");
            }
            finally { active.Remove(path); }
        }

        private void Parse(string line, string path, int number, string[] arguments, int depth)
        {
            if (line.Length == 0 || line.StartsWith(':') || line.StartsWith("::") ||
                line.Equals("rem", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("rem ", StringComparison.OrdinalIgnoreCase)) return;

            // Explicit dispatch names are useful module candidates, even though goto is not evaluated.
            foreach (Match match in ModuleDispatch.Matches(line))
                modules.Add(new(match.Groups["name"].Value.ToUpperInvariant(), new(path, number, "LaunchDispatch")));

            var conditional = Conditional.Match(line);
            if (conditional.Success)
            {
                var target = Expand(conditional.Groups["quoted"].Success
                    ? conditional.Groups["quoted"].Value : conditional.Groups["bare"].Value, path, arguments);
                if (target is null) { Note("UnresolvedExpression", path, $"Unresolved existence condition at line {number}."); return; }
                target = Resolve(target, path);
                var exists = files.FileExists(target) || files.DirectoryExists(target);
                if (exists != conditional.Groups["not"].Success)
                    Parse(conditional.Groups["command"].Value, path, number, arguments, depth);
                return;
            }
            if (line.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
            {
                var assignment = line[4..].Trim();
                if (assignment.StartsWith('"') && assignment.EndsWith('"')) assignment = assignment[1..^1];
                var equals = assignment.IndexOf('=');
                if (equals <= 0 || assignment[0] == '/') { Note("UnsupportedSet", path, $"Dynamic assignment at line {number} skipped."); return; }
                var key = assignment[..equals].Trim();
                if (SensitiveFields.ContainsSecret(key)) return;
                var value = Expand(assignment[(equals + 1)..], path, arguments);
                if (value is null) { Note("UnresolvedExpression", path, $"Unresolved assignment at line {number} skipped."); return; }
                variables[key] = value;
                Collect(key, value, new(path, number, "Assignment"));
                return;
            }
            if (line.StartsWith("call ", StringComparison.OrdinalIgnoreCase))
            {
                var tokens = Tokens.Matches(line[5..]).Select(token => Expand(token.Value.Trim('"'), path, arguments)).ToArray();
                if (tokens.Length == 0 || tokens.Any(token => token is null))
                { Note("UnresolvedCall", path, $"Unresolved include at line {number} skipped."); return; }
                var target = Resolve(tokens[0]!, path);
                if (Path.GetExtension(target).Length == 0) target += ".bat";
                if (Path.GetExtension(target).ToLowerInvariant() is ".bat" or ".cmd" or ".init")
                    Read(target, tokens.Skip(1).Select(token => token!).ToArray(), depth + 1);
                return;
            }
            if (line.StartsWith("if ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("goto ", StringComparison.OrdinalIgnoreCase) || line.StartsWith("for ", StringComparison.OrdinalIgnoreCase))
                Note("ControlFlowNotEvaluated", path, "Dynamic control flow is not executed; results are static candidates, not the effective runtime environment.");
        }

        private string? Expand(string value, string path, string[] arguments)
        {
            var directory = Path.GetDirectoryName(path)! + Path.DirectorySeparatorChar;
            value = Regex.Replace(value, "%~dp0", _ => directory, Flags);
            value = Regex.Replace(value, "%~dp1", _ => arguments.Length == 0 ? installation + Path.DirectorySeparatorChar
                : Path.GetDirectoryName(arguments[0].TrimEnd('\\') + "\\placeholder")! + "\\", Flags);
            value = Regex.Replace(value, "%~df1", _ => arguments.Length == 0 ? installation : arguments[0], Flags);
            value = value.Replace("%0", Path.GetExtension(path).Equals(".init", StringComparison.OrdinalIgnoreCase) ? installation : path);
            for (var index = 1; index <= 9; index++)
                value = value.Replace("%" + index, arguments.Length >= index ? arguments[index - 1] : "");
            var resolved = true;
            value = Variable.Replace(value, match =>
            {
                if (variables.TryGetValue(match.Groups[1].Value, out var replacement)) return replacement;
                resolved = false;
                return match.Value;
            });
            return resolved && value.IndexOfAny(['%', '!', '&', '|', '<', '>', '^']) < 0 ? value.Trim() : null;
        }

        private void Collect(string key, string value, DiscoveryOrigin origin)
        {
            var project = ProjectVariable.Match(key);
            if (project.Success && !string.IsNullOrWhiteSpace(value))
            {
                var directory = Resolve(value, origin.File);
                projects.Add(new(project.Groups[1].Value.ToUpperInvariant(), directory, files.DirectoryExists(directory), origin));
            }
            if (key.Equals("projects_dir", StringComparison.OrdinalIgnoreCase) || key.Equals("project_dir", StringComparison.OrdinalIgnoreCase))
                foreach (var directory in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    roots.Add(new(Resolve(directory, origin.File), origin));
            // Configuration references are still read explicitly; do not crawl known drawing/data stores.
            if (ContentVariable.IsMatch(key))
                foreach (var directory in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    contentDirectories.Add(Resolve(directory, origin.File));
            if (new[] { "module", "pdms_module", "moduleArg", "AVEVA_DESIGN_MODULE" }.Contains(key, StringComparer.OrdinalIgnoreCase) && Code.IsMatch(value))
                modules.Add(new(value.ToUpperInvariant(), origin));
        }

        private void WalkProjectDirectories()
        {
            var pending = new Queue<(string Directory, int Depth)>();
            var queuedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (CanContinue())
            {
                foreach (var root in roots.ToArray())
                    if (queuedRoots.Add(root.Directory)) pending.Enqueue((root.Directory, 0));
                if (!pending.TryDequeue(out var item)) break;
                if (contentDirectories.Contains(item.Directory)) continue;
                if (!visitedDirectories.Add(item.Directory)) continue;
                if (visitedDirectories.Count > options.MaximumDirectories)
                { Note("EnvironmentDirectoryLimit", item.Directory, "Project directory limit reached."); break; }
                try
                {
                    if (!files.DirectoryExists(item.Directory) || files.IsReparsePoint(item.Directory)) continue;
                    var name = Path.GetFileName(item.Directory.TrimEnd('\\', '/'));
                    var project = ProjectVariable.Match(name);
                    if (project.Success)
                    {
                        projects.Add(new(project.Groups[1].Value.ToUpperInvariant(), item.Directory, true,
                            new(item.Directory, 0, "DirectoryHint")));
                        continue; // Do not crawl database contents.
                    }
                    foreach (var file in files.EnumerateFiles(item.Directory))
                    {
                        if (!CanContinue()) break;
                        var fileName = Path.GetFileName(file);
                        if ((fileName.StartsWith("evars", StringComparison.OrdinalIgnoreCase) && fileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)) ||
                            new[] { "custom_evars.bat", "projects.bat", "launch.init", "evars.init" }.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                            if (!readFiles.Contains(Path.GetFullPath(file))) Read(file, [], 0);
                    }
                    if (item.Depth < options.MaximumDepth)
                        foreach (var directory in files.EnumerateDirectories(item.Directory))
                        {
                            if (!CanContinue()) break;
                            if (pending.Count >= options.MaximumDirectories)
                            {
                                Note("EnvironmentDirectoryLimit", item.Directory, "Pending project directory limit reached.");
                                break;
                            }
                            pending.Enqueue((directory, item.Depth + 1));
                        }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { Note("ProjectDirectoryReadFailed", item.Directory, "Project directory could not be inspected."); }
            }
        }

        private void FindModuleExecutables()
        {
            foreach (var (executable, module) in new[] { ("des.exe", "DESIGN"), ("adm.exe", "ADMIN"),
                ("dra.exe", "DRAFT"), ("iss.exe", "ISODRAFT"), ("lex.exe", "LEXICON") })
            {
                if (!CanContinue()) return;
                var path = Path.Combine(installation, executable);
                if (files.FileExists(path)) modules.Add(new(module, new(path, 0, "ExecutableHint")));
            }
        }

        private static string Resolve(string value, string source) =>
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(value.Trim().Trim('"'), Path.GetDirectoryName(source)!));
        private void Note(string code, string path, string message)
        {
            if (diagnostics.Count < 128 && !diagnostics.Any(item => item.Code == code && item.Path == path))
                diagnostics.Add(new(code, path, message));
        }
    }
}
