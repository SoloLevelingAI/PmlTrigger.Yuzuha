using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>公开资料来源的完整指纹；不保存源文件正文或原应用凭据。</summary>
public sealed record LaunchDeskImportSource(string File, string Sha256);
/// <summary>历史记录只保留结构坐标及受控字段，丢弃原 InputSnapshot/ArgumentPlan/Reason。</summary>
public sealed record LaunchDeskImportedHistory(long Sequence, string Timestamp, string AttemptId, string Stage, string Outcome,
    Dictionary<string, string> Fields);
/// <summary>未激活的导入草稿，保留原配置/快照坐标并产生本管理器的新身份。</summary>
public sealed record LaunchDeskImportedDraft(string SourceFile, string SourceId, string Kind, LaunchProfile Profile);
/// <summary>用户明确选择的原型资料预览；敏感环境、密码和凭据引用均不带入。</summary>
public sealed record LaunchDeskImportPlan(string SelectedSource, string DataRoot, LaunchDeskImportSource[] Sources,
    LaunchDeskImportedDraft[] Drafts, LaunchDeskImportedHistory[] History, string[] Warnings);
/// <summary>本机管理状态中保留的净化导入快照。</summary>
public sealed record LaunchDeskImportReceipt(string Id, string Directory, int Drafts, int History, string Sha256);

/// <summary>只读取所选 LaunchDesk portable/legacy/Next 数据，不启动原应用或读取其凭据库。</summary>
public static class LaunchDeskImport
{
    /// <summary>支持 application/data 目录或明确的 portable/workspace/profiles/draft 文件。</summary>
    public static LaunchDeskImportPlan Preview(string source, CancellationToken token = default)
    {
        if (!Path.IsPathFullyQualified(source) || source.StartsWith(@"\\", StringComparison.Ordinal)) throw new ArgumentException("请选择本机来源的绝对路径。");
        source = Path.GetFullPath(source); PathRules.NoLinks(source);
        var root = Directory.Exists(source) ? source : Path.GetDirectoryName(source)!;
        var sources = new List<LaunchDeskImportSource>(); var drafts = new List<LaunchDeskImportedDraft>();
        var history = new List<LaunchDeskImportedHistory>(); var warnings = new List<string>();
        long totalBytes = 0;
        var selectedFile = File.Exists(source) ? source : null;
        var portable = selectedFile is not null && Path.GetFileName(selectedFile).Equals("portable.json", StringComparison.OrdinalIgnoreCase) ? selectedFile :
            selectedFile is null && File.Exists(Path.Combine(root, "portable.json")) ? Path.Combine(root, "portable.json") : null;
        if (portable is not null)
        {
            using var document = Read(portable, 16384);
            var directory = Text(document.RootElement, "DataDirectory");
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidDataException("portable.json 缺少 DataDirectory。");
            root = Path.GetFullPath(directory, Path.GetDirectoryName(portable)!);
            if (root.StartsWith(@"\\", StringComparison.Ordinal)) throw new InvalidDataException("不隐式读取网络 portable 数据根。");
            PathRules.NoLinks(root); selectedFile = null;
        }
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("所选 LaunchDesk 数据目录不存在。");
        var legacy = selectedFile ?? Path.Combine(root, "workspace.json");
        if (Path.GetFileName(legacy).Equals("workspace.json", StringComparison.OrdinalIgnoreCase) && File.Exists(legacy))
        {
            using var lease = new FileStream(legacy, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var document = Read(legacy);
            var values = LaunchDrafts.ReadLaunchDesk(legacy); var rows = document.RootElement.GetProperty("Profiles").EnumerateArray().ToArray();
            for (var index = 0; index < values.Length; index++) AddDraft(legacy, Text(rows[index], "Id"), "legacy-profile", values[index]);
        }
        else
        {
            if (selectedFile is not null && Path.GetFileName(selectedFile) is not ("profiles.json" or "draft.json")) throw new ArgumentException("请选择 portable.json、workspace.json、profiles.json 或 draft.json。");
            var profiles = selectedFile ?? Path.Combine(root, "profiles.json");
            if (File.Exists(profiles))
            {
                using var document = Read(profiles); var value = document.RootElement;
                if (value.ValueKind == JsonValueKind.Array) foreach (var row in value.EnumerateArray()) AddNext(profiles, row, "next-profile");
                else if (value.ValueKind == JsonValueKind.Object) AddNext(profiles, value, "next-draft");
                else throw new InvalidDataException("Next 配置不是对象或数组。");
            }
            if (selectedFile is null)
            {
                var draft = Path.Combine(root, "draft.json");
                if (File.Exists(draft)) { using var document = Read(draft); AddNext(draft, document.RootElement, "next-draft"); }
                var snapshots = Path.Combine(root, "snapshots"); PathRules.NoLinks(snapshots);
                if (Directory.Exists(snapshots)) foreach (var file in Directory.EnumerateFiles(snapshots, "*.json").Order(StringComparer.OrdinalIgnoreCase))
                {
                    if (drafts.Count >= 2000) throw new IOException("草稿/历史快照超过 2000 条预算。");
                    using var document = Read(file); var value = document.RootElement;
                    AddNext(file, Field(value, "Model"), "next-snapshot");
                }
                var records = Path.Combine(root, "logs", "records.jsonl");
                if (File.Exists(records)) ReadHistory(records);
            }
        }
        if (drafts.Count == 0 && history.Count == 0) throw new InvalidDataException("所选来源没有可带入的配置草稿或历史。");
        warnings.Add("草稿未激活；需检查本机路径后用 launch.profile.preview/save 保存。未复制密码、凭据引用、环境变量、嵌套输入快照及命令参数。");
        return new(source, root, sources.OrderBy(s => s.File, StringComparer.OrdinalIgnoreCase).ToArray(), drafts.ToArray(), history.ToArray(), warnings.ToArray());

        JsonDocument Read(string file, int budget = 2 * 1024 * 1024)
        {
            token.ThrowIfCancellationRequested(); PathRules.NoLinks(file);
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > budget || sources.Count > 2100 || (totalBytes += stream.Length) > 32 * 1024 * 1024) throw new IOException("原型资料超过读取预算。");
            var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes);
            sources.Add(new(file, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()));
            return JsonDocument.Parse(bytes.AsMemory(bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 }) ? 3 : 0));
        }
        void AddDraft(string file, string sourceId, string kind, LaunchProfile profile)
        {
            if (drafts.Count >= 2000) throw new IOException("草稿超过 2000 条预算。");
            var identity = file + "\0" + sourceId + "\0" + kind + "\0" + drafts.Count;
            profile.Id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant()[..32];
            profile.CredentialKey = ""; profile.RememberPassword = false; profile.Environment.Clear(); profile.ExtensionData = null;
            profile.IsDefault = false; profile.IsFavorite = false; profile.LastStartedUtc = null;
            drafts.Add(new(file, sourceId, kind, profile));
        }
        void AddNext(string file, JsonElement value, string kind)
        {
            token.ThrowIfCancellationRequested();
            var wrappedModel = Field(value, "Model");
            if (wrappedModel.ValueKind == JsonValueKind.Object) value = wrappedModel;
            var schema = Field(value, "SchemaVersion");
            if (schema.ValueKind != JsonValueKind.Undefined && (schema.ValueKind != JsonValueKind.Number || schema.GetInt32() != 1))
                throw new InvalidDataException("Next 草稿版本不受支持：" + file);
            var protocol = Field(value, "Protocol");
            var number = protocol.ValueKind == JsonValueKind.Number ? protocol.GetInt32() : -1;
            var name = protocol.ValueKind == JsonValueKind.String ? protocol.GetString() : "";
            var entryKind = number == 0 || name == "Monitor" ? "Monitor" : number == 1 || name == "Marine" ? "MarineBatch" : null;
            if (entryKind is null) { warnings.Add("跳过尚不支持的 Next Direct 入口：" + file); return; }
            AddDraft(file, Text(value, "ConfigurationId"), kind, new LaunchProfile {
                Name = Text(value, "DisplayName"), Executable = Text(value, "ExecutablePath"), WorkingDirectory = Text(value, "WorkingDirectory"),
                Initialization = Text(value, "InitializationFile"), Kind = entryKind, Product = Text(value, "Product"),
                Project = Text(value, "Project"), User = Text(value, "User"), Mdb = Text(value, "Mdb"), Module = Text(value, "Module"), License = Text(value, "LicenceServer") });
        }
        void ReadHistory(string file)
        {
            token.ThrowIfCancellationRequested(); PathRules.NoLinks(file);
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 8 * 1024 * 1024 || (totalBytes += stream.Length) > 32 * 1024 * 1024) throw new IOException("Next 历史或总资料超过预算，请先选择较小资料副本。");
            var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes);
            sources.Add(new(file, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()));
            using var reader = new StringReader(new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF')); var damaged = 0;
            while (reader.ReadLine() is { } line)
            {
                token.ThrowIfCancellationRequested(); if (string.IsNullOrWhiteSpace(line)) continue;
                if (history.Count >= 10000) throw new IOException("Next 历史超过 10000 条预算。");
                try
                {
                    using var document = JsonDocument.Parse(line); var row = document.RootElement;
                    var fields = new Dictionary<string, string>(); var snapshot = Field(row, "Snapshot");
                    foreach (var key in new[] { "ConfigurationId", "Version", "ExecutablePath", "Project", "User", "Mdb", "Module", "ProcessId" })
                    { var value = Text(snapshot, key); if (value.Length > 0) fields[key] = value; }
                    var seq = Field(row, "Sequence"); var timestamp = Text(row, "Timestamp");
                    if (!DateTimeOffset.TryParse(timestamp, out _) || !Guid.TryParse(Text(row, "AttemptId"), out var attempt)) { damaged++; continue; }
                    history.Add(new(seq.ValueKind == JsonValueKind.Number ? seq.GetInt64() : 0, timestamp, attempt.ToString("D"), SafeLabel(Text(row, "Stage")), SafeLabel(Text(row, "Outcome")), fields));
                }
                catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or OverflowException) { damaged++; }
            }
            if (damaged != 0) warnings.Add("历史有 " + damaged + " 条损坏记录，已明确跳过；原文件保留。");
        }
    }

    /// <summary>来源未变化后发布净化草稿/历史快照；不写入启动配置或原型数据根。</summary>
    public static LaunchDeskImportReceipt Apply(LaunchDeskImportPlan plan, string stateDirectory, CancellationToken token = default)
    {
        var fresh = Preview(plan.SelectedSource, token);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(plan, ManagerJsonContext.Default.LaunchDeskImportPlan);
        if (!bytes.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(fresh, ManagerJsonContext.Default.LaunchDeskImportPlan))) throw new IOException("原型资料已变化，请重新预览。");
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(); var id = hash[..32];
        stateDirectory = Path.GetFullPath(stateDirectory);
        if (UserDataPaths.Contains(plan.DataRoot, stateDirectory)) throw new IOException("管理状态目录不能位于原型数据根内。");
        var root = Path.Combine(stateDirectory, "LaunchDeskImports"); PathRules.NoLinks(root); Directory.CreateDirectory(root);
        var target = PathRules.Inside(root, id); var output = Path.Combine(target, "import.json");
        if (Directory.Exists(target))
        {
            if (!File.Exists(output) || !File.ReadAllBytes(output).SequenceEqual(bytes)) throw new IOException("同名导入快照发生冲突。");
            return new(id, target, plan.Drafts.Length, plan.History.Length, hash);
        }
        var stage = Path.Combine(root, ".stage-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
        try
        {
            using (var file = new FileStream(Path.Combine(stage, "import.json"), FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(bytes); file.Flush(true); }
            token.ThrowIfCancellationRequested(); Directory.Move(stage, target);
            return new(id, target, plan.Drafts.Length, plan.History.Length, hash);
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
    }
    private static JsonElement Field(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var field) ? field : default;
    private static string Text(JsonElement value, string key) { var field = Field(value, key); return field.ValueKind == JsonValueKind.String ? field.GetString()! : ""; }
    private static string SafeLabel(string value) => value.Length <= 80 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-') ? value : "unrecognized";
}
