using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
namespace RibbonStudio.Backend
{
    public sealed class ManagementFeature
    {
        readonly WorkspaceFiles store; readonly WorkspaceFeature workspace; readonly object gate = Data.WriteGate; public ManagementFeature(WorkspaceFiles files, WorkspaceFeature feature)
        {
            store = files;
            workspace = feature;
        }
        bool SameBinding(Binding b, ManifestFile m, string name)
        {
            return Data.Same(b.monPath, m.Mon) && String.Equals(b.manifestName, m.Name, StringComparison.OrdinalIgnoreCase) && b.entryName == name;
        }
        public object Inventory(string mon, string manifest)
        {
            lock (gate)
            {
                var m = Manifests.Load(mon, manifest);
                Migrate(m);
                var docs = store.List();
                var rows = new List<Entry>();
                foreach (var e in m.Entries)
                {
                    string name = e.GetAttribute("Name"), path = e.GetAttribute("Path"), file = Manifests.Actual(m, path);
                    byte[] bytes = null;
                    try
                    {
                        if (file != "")
                            bytes = Data.Read(file);
                    }
                    catch { }
                    var owner = docs.FirstOrDefault(d => store.Bindings(Data.Text(d, "id")).Any(b => SameBinding(b, m, name) && b.registrationPath == path && Data.Same(b.filePath, file)));
                    string origin = owner != null ? "managed" : Regex.IsMatch(name, @"^RibbonStudio\.[a-f0-9]{16}$", RegexOptions.IgnoreCase) ? "unknown" : file == "" || Manifests.Inside(file, m.Directory) ? "protected" : bytes != null && Path.IsPathRooted(path) ? "custom" : "unknown";
                    rows.Add(new Entry { key = Data.Hash(name + "\0" + path), name = name, path = path, filePath = file, origin = origin, template = Regex.IsMatch(path, "[$%]"), reason = origin == "managed" ? "有部署器持久化所有权记录" : origin == "custom" ? "既有自定义 UIC，可编辑；接管后可管理加载" : "原生或无法确认来源，只读保护", exists = bytes != null, enabled = true, documentId = owner == null ? null : Data.Text(owner, "id"), manifestHash = Data.Hash(m.Bytes), uicHash = bytes == null ? null : Data.Hash(bytes) });
                }
                foreach (var doc in docs)
                    foreach (var b in store.Bindings(Data.Text(doc, "id")))
                        if (SameBinding(b, m, b.entryName) && !rows.Any(r => r.name == b.entryName && r.path == b.registrationPath))
                        {
                            var bytes = Data.Read(b.filePath);
                            rows.Add(new Entry { key = Data.Hash(b.entryName + "\0" + b.registrationPath), name = b.entryName, path = b.registrationPath, filePath = b.filePath, origin = "managed", reason = "已移除加载；文件和历史保留", exists = bytes != null, enabled = false, documentId = Data.Text(doc, "id"), manifestHash = Data.Hash(m.Bytes), uicHash = bytes == null ? null : Data.Hash(bytes) });
                        }
                return new
                {
                    rows,
                    monPath = m.Mon,
                    manifestPath = m.Path,
                    manifestHash = Data.Hash(m.Bytes)
                };
            }
        }
        void Migrate(ManifestFile m)
        {
            string root = Path.Combine(m.Directory, ".ribbon-studio-backups");
            if (!Directory.Exists(root))
                return;
            foreach (var folder in Directory.GetDirectories(root))
                try
                {
                    string file = Path.Combine(folder, "receipt.json");
                    if (!File.Exists(file))
                        continue;
                    var receipt = Json.Read<object>(File.ReadAllText(file));
                    string path = Data.Text(receipt, "uicPath"), name = Data.Text(receipt, "entryName");
                    if (!Data.Same(Data.Text(receipt, "manifestPath"), m.Path) || !Data.Same(Path.GetDirectoryName(path), m.Directory) || !Regex.IsMatch(Path.GetFileName(path), @"^RibbonStudio\.[a-f0-9]{16}\.uic$", RegexOptions.IgnoreCase))
                        continue;
                    if (store.List().Any(d => store.Bindings(Data.Text(d, "id")).Any(b => SameBinding(b, m, name))))
                        continue;
                    var entry = m.Entries.FirstOrDefault(e => e.GetAttribute("Name") == name && Data.Same(Manifests.Actual(m, e.GetAttribute("Path")), path));
                    var bytes = Data.Read(path);
                    if (entry == null || bytes == null || Data.Hash(bytes) != Data.Text(receipt, "nextUicHash"))
                        continue;
                    var imported = workspace.Import(new Dictionary<string, object> { { "path", path } });
                    store.Bind(imported.project.documentId, new Binding { monPath = m.Mon, manifestName = m.Name, entryName = name, registrationPath = entry.GetAttribute("Path"), filePath = path });
                    store.Save(imported.project, "legacy-adopt", "验证旧部署凭证与 UIC 摘要后迁移", new
                    {
                        receipt = Json.Write(receipt)
                    });
                }
                catch {/* Unverified receipts do not grant ownership. */}
        }
        Entry Find(object input)
        {
            var inventory = Data.Map(Json.Read<object>(Json.Write(Inventory(Data.Text(input, "monPath"), Data.Text(input, "manifestName")))));
            if (Data.Get(input, "expectedManifestHash") != null && Data.Text(inventory, "manifestHash") != Data.Text(input, "expectedManifestHash"))
                throw new ApiError(409, "清单已变化，请重新扫描");
            var row = Data.Items(inventory["rows"]).Select(Data.As<Entry>).FirstOrDefault(r => r.key == Data.Text(input, "key"));
            if (row == null || row.origin != "custom" && row.origin != "managed" || String.IsNullOrEmpty(row.filePath))
                throw new ApiError(403, "原生/未知条目只读");
            return row;
        }
        Project ExistingOrImport(Entry row, string documentId)
        {
            var record = store.List().FirstOrDefault(d => Data.Same(Data.Text(d, "sourcePath"), row.filePath));
            if (record == null && !String.IsNullOrEmpty(documentId))
            {
                var candidate = Data.As<Project>(store.Version(documentId)["project"]);
                System.Text.Encoding encoding;
                bool bom;
                var bytes = Data.Read(row.filePath);
                if (bytes != null && candidate.origin != null && candidate.origin.sourceHash == Data.Hash(Manifests.Decode(bytes, out encoding, out bom)))
                    record = store.Get(documentId);
            }
            return record == null ? workspace.Import(new Dictionary<string, object> { { "path", row.filePath } }).project : Data.As<Project>(store.Version(Data.Text(record, "id"))["project"]);
        }
        public object Open(object input)
        {
            var row = Find(input);
            var p = ExistingOrImport(row, Data.Text(input, "documentId"));
            if (p.origin != null)
                p.origin.sourcePath = row.filePath;
            p.preferredBinding = new Binding { monPath = Data.Text(input, "monPath"), manifestName = Data.Text(input, "manifestName"), entryName = row.name };
            return p;
        }
        public object Adopt(object input)
        {
            lock (gate)
            {
                var row = Find(input);
                var m = Manifests.Load(Data.Text(input, "monPath"), Data.Text(input, "manifestName"));
                if (m.Entries.Count(e => e.GetAttribute("Name") == row.name) != 1)
                    throw new ApiError(409, "同名条目有歧义");
                var p = ExistingOrImport(row, Data.Text(input, "documentId"));
                store.Save(p, "adopt-baseline", "接管保留当前草稿与原 UIC 字节", new
                {
                    originalUicBase64 = Convert.ToBase64String(Data.Read(row.filePath))
                });
                if (p.origin != null)
                    p.origin.sourcePath = row.filePath;
                var binding = new Binding { monPath = m.Mon, manifestName = m.Name, entryName = row.name, registrationPath = row.path, filePath = row.filePath };
                store.Bind(p.documentId, binding);
                return store.Save(p, "adopt", "明确接管加载项 " + row.name, new
                {
                    binding = Json.Write(binding)
                });
            }
        }
        public object Toggle(object input)
        {
            lock (gate)
            {
                if (!(Data.Get(input, "enabled") is bool))
                    throw new ApiError(422, "加载状态必须为布尔值");
                var row = Find(input);
                if (row.origin != "managed")
                    throw new ApiError(403, "只有托管项可以修改加载状态");
                bool enabled = (bool)Data.Get(input, "enabled");
                if (enabled == row.enabled)
                    return new
                    {
                        unchanged = true
                    };
                var m = Manifests.Load(Data.Text(input, "monPath"), Data.Text(input, "manifestName"));
                string next;
                if (enabled)
                {
                    if (!row.exists)
                        throw new ApiError(409, "UIC 文件不存在");
                    next = Manifests.Patch(m.Xml, row.name, row.path);
                }
                else
                {
                    var matches = m.Entries.Where(e => e.GetAttribute("Name") == row.name && e.GetAttribute("Path") == row.path).ToList();
                    if (matches.Count != 1)
                        throw new ApiError(409, "加载条目有歧义");
                    var node = matches[0];
                    var doc = node.OwnerDocument;
                    node.ParentNode.RemoveChild(node);
                    next = doc.OuterXml;
                }
                var p = Data.As<Project>(store.Version(row.documentId)["project"]);
                store.Save(p, "registration-start", "修改加载前快照", new
                {
                    manifestPath = m.Path,
                    manifestBeforeBase64 = Convert.ToBase64String(m.Bytes)
                });
                if (Data.Hash(Data.Read(m.Path)) != Data.Text(input, "expectedManifestHash"))
                    throw new ApiError(409, "清单变化，未写入");
                string backup = m.Path + ".RibbonStudio." + Guid.NewGuid() + ".bak";
                File.WriteAllBytes(backup, m.Bytes);
                Data.Atomic(m.Path, m.Encode(next));
                store.Save(p, enabled ? "enable" : "disable", row.name + (enabled ? " 恢复加载" : " 移除加载（保留 UIC）"), new
                {
                    backup,
                    manifestAfterBase64 = Convert.ToBase64String(m.Encode(next))
                });
                return new
                {
                    unchanged = false,
                    backup
                };
            }
        }
    }
}
