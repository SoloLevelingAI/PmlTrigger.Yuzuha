using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
namespace RibbonStudio.Backend
{
    public sealed class DeploymentFeature
    {
        readonly WorkspaceFiles store; readonly object writes = Data.WriteGate; public DeploymentFeature(WorkspaceFiles files)
        {
            store = files;
        }
        Project ProjectOf(object input)
        {
            return Data.As<Project>(Data.Get(input, "project"));
        }
        List<Binding> References(Project p, string source, List<string> errors)
        {
            var paths = Manifests.Discover().Select(t => t.monPath).Concat(store.Bindings(p.documentId).Select(b => b.monPath)).ToList();
            if (p.preferredBinding != null)
                paths.Add(p.preferredBinding.monPath);
            var result = new List<Binding>();
            foreach (var mon in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Target target;
                try
                {
                    target = Manifests.Inspect(mon);
                }
                catch { errors.Add(mon); continue; }
                if (Manifests.Inside(source, target.directory) && !store.Bindings(p.documentId).Any(b => Data.Same(b.filePath, source)))
                    throw new ApiError(403, "安装目录内未托管的原生 UIC 只读");
                foreach (var name in target.manifests)
                    try
                    {
                        var m = Manifests.Load(mon, name);
                        foreach (var e in m.Entries)
                            if (Data.Same(Manifests.Actual(m, e.GetAttribute("Path")), source))
                                result.Add(new Binding { monPath = target.monPath, manifestName = name, entryName = e.GetAttribute("Name"), registrationPath = e.GetAttribute("Path"), filePath = source });
                    }
                    catch { errors.Add(Path.Combine(target.directory, name)); }
            }
            return result;
        }
        public Dictionary<string, object> FilePreview(Project p)
        {
            if (p == null || String.IsNullOrEmpty(p.documentId))
                throw new ApiError(422, "请先保存或导入 UIC");
            var doc = store.Get(p.documentId);
            string path = Data.Text(doc, "sourcePath");
            if (!Path.IsPathRooted(path) || !path.EndsWith(".uic", StringComparison.OrdinalIgnoreCase))
                throw new ApiError(422, "此文档没有本机原文件路径");
            var errors = new List<string>();
            var refs = References(p, path, errors);
            var before = Data.Read(path);
            if (before == null)
                throw new ApiError(404, "原 UIC 不存在");
            var next = Encoding.UTF8.GetBytes(UicFiles.Export(p));
            return new Dictionary<string, object> { { "operation", "update-file" }, { "uicPath", path }, { "uicHash", Data.Hash(before) }, { "nextUicHash", Data.Hash(next) }, { "uicBytes", next.Length }, { "unchanged", before.SequenceEqual(next) }, { "references", refs }, { "scanErrors", errors } };
        }
        public object FileApply(object input)
        {
            lock (writes)
            {
                var saved = store.Save(ProjectRules.Normalize(ProjectOf(input)), "save", "更新 UIC 前保存");
                var p = saved.project;
                var plan = FilePreview(p);
                string path = Data.Text(plan, "uicPath"), expected = Data.Text(input, "expectedUicHash");
                var before = Data.Read(path);
                if (before == null || Data.Hash(before) != expected || Data.Text(plan, "uicHash") != expected)
                    throw new ApiError(409, "UIC 文件已变化，请重新预览");
                if ((bool)plan["unchanged"])
                    return plan;
                var next = Encoding.UTF8.GetBytes(UicFiles.Export(p));
                string backup = Backup(path, before);
                store.Save(p, "deploy-start", "更新共享 UIC 前快照；模块清单保持不变", new
                {
                    uicBeforeBase64 = Convert.ToBase64String(before),
                    plan = Json.Write(plan),
                    backupDirectory = backup
                });
                if (Data.Hash(Data.Read(path)) != expected)
                    throw new ApiError(409, "UIC 文件已变化，未覆盖");
                Data.Atomic(path, next);
                foreach (var b in Data.As<List<Binding>>(plan["references"]))
                    store.Bind(p.documentId, b);
                store.Save(p, "deploy", "更新 UIC：" + path, new
                {
                    uicBeforeBase64 = Convert.ToBase64String(before),
                    uicAfterBase64 = Convert.ToBase64String(next),
                    backupDirectory = backup
                });
                plan["backupDirectory"] = backup;
                plan["documentId"] = p.documentId;
                return plan;
            }
        }
        public object Modules(object input)
        {
            var p = ProjectOf(input);
            if (p == null || String.IsNullOrEmpty(p.documentId))
                throw new ApiError(422, "请先保存或导入 UIC");
            string source = Data.Text(store.Get(p.documentId), "sourcePath");
            if (source == "")
                throw new ApiError(422, "文档没有本机 UIC 路径");
            var target = Manifests.Inspect(Data.Text(input, "monPath"));
            var rows = new List<object>();
            foreach (var name in target.manifests)
                try
                {
                    var m = Manifests.Load(target.monPath, name);
                    rows.Add(new
                    {
                        manifestName = name,
                        entryNames = m.Entries.Where(e => Data.Same(Manifests.Actual(m, e.GetAttribute("Path")), source)).Select(e => e.GetAttribute("Name")).ToList(),
                        error = ""
                    });
                }
                catch (Exception error) { rows.Add(new { manifestName = name, entryNames = new string[0], error = error.Message }); }
            return new
            {
                monPath = target.monPath,
                uicPath = source,
                modules = rows
            };
        }
        public object Registration(object input, bool apply)
        {
            lock (writes)
            {
                string name = Data.Text(input, "entryName");
                if (!Regex.IsMatch(name, @"^[\p{L}\p{N}_. -]{1,100}$"))
                    throw new ApiError(422, "请输入有效加载名称");
                var p = ProjectOf(input);
                var file = FilePreview(p);
                var m = Manifests.Load(Data.Text(input, "monPath"), Data.Text(input, "manifestName"));
                string source = Data.Text(file, "uicPath");
                if (m.Entries.Any(e => Data.Same(Manifests.Actual(m, e.GetAttribute("Path")), source) && e.GetAttribute("Name") != name))
                    throw new ApiError(409, "此模块已引用该 UIC，无需重复加入");
                string next = Manifests.Patch(m.Xml, name, source);
                var beforeEntries = EntryRows(m.Xml);
                var afterEntries = EntryRows(next);
                var result = new Dictionary<string, object> { { "manifestPath", m.Path }, { "manifestHash", Data.Hash(m.Bytes) }, { "uicPath", source }, { "uicHash", file["uicHash"] }, { "entryName", name }, { "unchanged", next == m.Xml }, { "beforeXml", m.Xml }, { "afterXml", next }, { "beforeEntries", beforeEntries }, { "afterEntries", afterEntries }, { "addedEntries", afterEntries.Where(e => !beforeEntries.Any(b => b.name == e.name && b.path == e.path)).ToList() }, { "nextManifestHash", Data.Hash(m.Encode(next)) }, { "uicModified", false } };
                if (!apply)
                    return result;
                if (Data.Hash(Data.Read(m.Path)) != Data.Text(input, "expectedManifestHash") || Data.Hash(Data.Read(source)) != Data.Text(input, "expectedUicHash"))
                    throw new ApiError(409, "文件已变化，请重新预览关联");
                var binding = new Binding { monPath = m.Mon, manifestName = m.Name, entryName = name, registrationPath = source, filePath = source };
                if (next != m.Xml)
                {
                    store.Save(p, "registration-start", "加入模块前快照", new
                    {
                        manifestPath = m.Path,
                        manifestBeforeBase64 = Convert.ToBase64String(m.Bytes),
                        binding = Json.Write(binding)
                    });
                    string backup = m.Path + ".RibbonStudio." + Guid.NewGuid() + ".bak";
                    File.WriteAllBytes(backup, m.Bytes);
                    Data.Atomic(m.Path, m.Encode(next));
                    result["backup"] = backup;
                }
                store.Bind(p.documentId, binding);
                store.Save(p, "enable", "加入模块：" + m.Name + "；UIC 内容保持不变", new
                {
                    binding = Json.Write(binding),
                    manifestAfterBase64 = Convert.ToBase64String(m.Encode(next))
                });
                return result;
            }
        }
        public static List<RegistrationEntry> EntryRows(string xml)
        {
            return Manifests.Entries(xml).Select(e => new RegistrationEntry { name = e.GetAttribute("Name"), path = e.GetAttribute("Path") }).ToList();
        }
        public static string Backup(string path, byte[] bytes)
        {
            string folder = Path.Combine(Path.GetDirectoryName(path), ".ribbon-studio-backups", Guid.NewGuid().ToString());
            Directory.CreateDirectory(folder);
            if (bytes != null)
                File.WriteAllBytes(Path.Combine(folder, Path.GetFileName(path)), bytes);
            return folder;
        }
        sealed class Prepared
        {
            public Dictionary<string, object> Plan; public Project Project; public ManifestFile Manifest; public byte[] Before, Uic, Next; public string Path, Name, Registration;
        }
        Prepared Prepare(object input)
        {
            var p = ProjectOf(input);
            ProjectRules.Require(p);
            var m = Manifests.Load(Data.Text(input, "monPath"), Data.Text(input, "manifestName"));
            Binding binding = null;
            if (!String.IsNullOrEmpty(p.documentId))
            {
                var matches = store.Bindings(p.documentId).Where(b => Data.Same(b.monPath, m.Mon) && String.Equals(b.manifestName, m.Name, StringComparison.OrdinalIgnoreCase)).ToList();
                if (p.preferredBinding != null)
                    matches = matches.Where(b => b.entryName == p.preferredBinding.entryName).ToList();
                if (matches.Count > 1)
                    throw new ApiError(409, "请在管理表选择具体托管条目");
                binding = matches.FirstOrDefault();
            }
            if (binding == null && p.origin != null)
            {
                var matches = m.Entries.Where(e => Data.Same(Manifests.Actual(m, e.GetAttribute("Path")), p.origin.sourcePath)).ToList();
                if (matches.Count != 1)
                    throw new ApiError(409, "所选模块未唯一引用原 UIC，不会另建哈希副本");
                var originalEntry = matches[0];
                string actual = Manifests.Actual(m, originalEntry.GetAttribute("Path"));
                if (Manifests.Inside(actual, m.Directory) || !Path.IsPathRooted(originalEntry.GetAttribute("Path")))
                    throw new ApiError(403, "原生/未知来源项只读");
                binding = new Binding { entryName = originalEntry.GetAttribute("Name"), registrationPath = originalEntry.GetAttribute("Path"), filePath = actual };
            }
            string name = binding == null ? "RibbonStudio." + Data.Hash(p.@namespace).Substring(0, 16) : binding.entryName, registration = binding == null ? name + ".uic" : binding.registrationPath, path = binding == null ? Path.Combine(m.Directory, registration) : binding.filePath;
            var old = Data.Read(path);
            if (old != null && binding == null && !m.Entries.Any(e => e.GetAttribute("Name") == name && e.GetAttribute("Path") == registration))
                throw new ApiError(409, "UIC 已存在但未登记为本项目，未覆盖");
            string xml = Manifests.Patch(m.Xml, name, registration);
            byte[] next = m.Encode(xml), uic = Encoding.UTF8.GetBytes(UicFiles.Export(p));
            var references = new List<string>();
            foreach (var manifest in Manifests.Inspect(m.Mon).manifests)
                try
                {
                    var other = Manifests.Load(m.Mon, manifest);
                    references.AddRange(other.Entries.Where(e => Data.Same(Manifests.Actual(other, e.GetAttribute("Path")), path)).Select(e => manifest + " / " + e.GetAttribute("Name")));
                }
                catch { }
            var plan = new Dictionary<string, object> { { "monPath", m.Mon }, { "manifestPath", m.Path }, { "uicPath", path }, { "entryName", name }, { "registration", new System.Xml.Linq.XElement("CustomizationFile", new System.Xml.Linq.XAttribute("Name", name), new System.Xml.Linq.XAttribute("Path", registration)).ToString() }, { "manifestHash", Data.Hash(m.Bytes) }, { "uicHash", old == null ? null : Data.Hash(old) }, { "nextManifestHash", Data.Hash(next) }, { "nextUicHash", Data.Hash(uic) }, { "uicBytes", uic.Length }, { "replacesUic", old != null }, { "unchanged", old != null && old.SequenceEqual(uic) && next.SequenceEqual(m.Bytes) }, { "managed", binding != null }, { "operation", binding == null ? "new" : "overwrite-original" }, { "references", references } };
            return new Prepared { Plan = plan, Project = p, Manifest = m, Before = old, Uic = uic, Next = next, Path = path, Name = name, Registration = registration };
        }
        public object Preview(object input)
        {
            return Prepare(input).Plan;
        }
        public object Apply(object input)
        {
            lock (writes)
            {
                var p = ProjectRules.Normalize(ProjectOf(input));
                var saved = store.Save(p, "save", "部署前保存");
                var data = Data.Map(Json.Read<object>(Json.Write(input)));
                data["project"] = saved.project;
                var prepared = Prepare(data);
                var plan = prepared.Plan;
                var m = prepared.Manifest;
                if (Data.Text(plan, "manifestHash") != Data.Text(input, "expectedManifestHash") || Data.Text(plan, "uicHash", null) != Data.Text(input, "expectedUicHash", null))
                    throw new ApiError(409, "预览已过期，请重新预览");
                if ((bool)plan["unchanged"])
                    return plan;
                string folder = Backup(m.Path, m.Bytes);
                if (prepared.Before != null)
                    File.WriteAllBytes(Path.Combine(folder, Path.GetFileName(prepared.Path)), prepared.Before);
                var receipt = new Dictionary<string, object>(plan);
                receipt["createdAt"] = Data.Now();
                Data.AtomicJson(Path.Combine(folder, "receipt.json"), receipt);
                store.Save(saved.project, "deploy-start", "部署前持久化快照", new
                {
                    manifestBeforeBase64 = Convert.ToBase64String(m.Bytes),
                    uicBeforeBase64 = prepared.Before == null ? "" : Convert.ToBase64String(prepared.Before),
                    plan = Json.Write(plan)
                });
                if (Data.Hash(Data.Read(m.Path)) != Data.Text(plan, "manifestHash") || (Data.Read(prepared.Path) == null ? null : Data.Hash(Data.Read(prepared.Path))) != Data.Text(plan, "uicHash", null))
                    throw new ApiError(409, "目标发生变化，未覆盖");
                bool uicWritten = false, manifestWritten = false;
                try
                {
                    Data.Atomic(prepared.Path, prepared.Uic);
                    uicWritten = true;
                    Data.Atomic(m.Path, prepared.Next);
                    manifestWritten = true;
                    if (Data.Hash(Data.Read(prepared.Path)) != Data.Text(plan, "nextUicHash") || Data.Hash(Data.Read(m.Path)) != Data.Text(plan, "nextManifestHash"))
                        throw new IOException("写入后文件校验失败");
                }
                catch { if (manifestWritten) Data.Atomic(m.Path, m.Bytes); if (uicWritten) { if (prepared.Before == null) File.Delete(prepared.Path); else Data.Atomic(prepared.Path, prepared.Before); } throw; }
                store.Bind(saved.project.documentId, new Binding { monPath = m.Mon, manifestName = m.Name, entryName = prepared.Name, registrationPath = prepared.Registration, filePath = prepared.Path });
                store.Save(saved.project, "deploy", "部署完成：" + prepared.Path, new
                {
                    uicAfterBase64 = Convert.ToBase64String(prepared.Uic),
                    manifestAfterBase64 = Convert.ToBase64String(prepared.Next)
                });
                plan["backupDirectory"] = folder;
                plan["documentId"] = saved.project.documentId;
                return plan;
            }
        }
    }
}
