using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
namespace RibbonStudio.Backend
{
    public sealed class WorkspaceFiles
    {
        public readonly string Root; readonly object gate = new object();
        public WorkspaceFiles(string root)
        {
            Root = Path.GetFullPath(root);
            Directory.CreateDirectory(Path.Combine(Root, "documents"));
        }
        string Folder(string id)
        {
            Guid value;
            if (!Guid.TryParseExact(id, "D", out value))
                throw new ApiError(422, "无效记录标识");
            return Path.Combine(Root, "documents", id);
        }
        public Dictionary<string, object> Get(string id)
        {
            string file = Path.Combine(Folder(id), "document.json");
            if (!File.Exists(file))
                throw new ApiError(404, "历史记录不存在");
            return Json.Read<Dictionary<string, object>>(File.ReadAllText(file));
        }
        public List<Dictionary<string, object>> List()
        {
            lock (gate)
                return Directory.GetDirectories(Path.Combine(Root, "documents")).Where(f => File.Exists(Path.Combine(f, "document.json"))).Select(f => Get(Path.GetFileName(f))).OrderByDescending(d => Data.Text(d, "updatedAt")).ToList();
        }
        public List<Dictionary<string, object>> Versions(string id)
        {
            lock (gate)
            {
                Get(id);
                return Directory.GetFiles(Path.Combine(Folder(id), "versions"), "*.json").Select(f => Json.Read<Dictionary<string, object>>(File.ReadAllText(f))).OrderByDescending(v => Convert.ToInt32(v["sequence"])).ToList();
            }
        }
        public Dictionary<string, object> Version(string id, string versionId = null)
        {
            lock (gate)
            {
                var doc = Get(id);
                string actual = String.IsNullOrEmpty(versionId) ? Data.Text(doc, "lastVersion") : versionId;
                Guid parsed;
                if (!Guid.TryParseExact(actual, "D", out parsed))
                    throw new ApiError(422, "无效版本标识");
                string file = Path.Combine(Folder(id), "versions", actual + ".json");
                if (!File.Exists(file))
                    throw new ApiError(404, "版本不存在");
                return Json.Read<Dictionary<string, object>>(File.ReadAllText(file));
            }
        }
        void Prune(Dictionary<string, object> doc)
        {
            var all = Directory.GetFiles(Path.Combine(Folder(Data.Text(doc, "id")), "versions"), "*.json").Select(f => Json.Read<Dictionary<string, object>>(File.ReadAllText(f))).OrderByDescending(v => Convert.ToInt32(v["sequence"])).ToList();
            foreach (var old in all.Where(v => Data.Text(v, "kind") == "edit" && Data.Get(v, "artifacts") == null).Skip(5).ToList())
            {
                File.Delete(Path.Combine(Folder(Data.Text(doc, "id")), "versions", Data.Text(old, "id") + ".json"));
                all.Remove(old);
            }
            doc["count"] = all.Count;
            doc["latestSequence"] = Math.Max(Convert.ToInt32(Data.Get(doc, "latestSequence") ?? 0), all.Count == 0 ? 0 : all.Max(v => Convert.ToInt32(v["sequence"])));
            Data.AtomicJson(Path.Combine(Folder(Data.Text(doc, "id")), "document.json"), doc);
        }
        public SaveResult Save(Project input, string kind, string note, object artifacts = null)
        {
            lock (gate)
            {
                if (input == null || input.schemaVersion != 1 || input.buttons == null || input.tabs == null || input.tabs.Any(t => t == null || t.groups == null || t.groups.Any(g => g == null || g.items == null || g.items.Any(i => i == null || i.size != "large" && i.size != "small"))))
                    throw new ApiError(422, "草稿结构不支持恢复");
                var p = Json.Copy(input);
                bool existing = !String.IsNullOrEmpty(p.documentId);
                if (!existing)
                    p.documentId = Guid.NewGuid().ToString();
                var meta = existing ? Get(p.documentId) : new Dictionary<string, object> { { "id", p.documentId }, { "bindings", new object[0] }, { "count", 0 }, { "latestSequence", 0 }, { "lastVersion", "" } };
                if (!existing && p.origin != null)
                {
                    meta["sourcePath"] = p.origin.sourcePath;
                    meta["sourceName"] = p.origin.fileName;
                }
                string hash = Data.Hash(Data.CanonicalJson(p));
                if (artifacts == null && Data.Text(meta, "lastVersion") != "" && (kind == "save" || kind == "edit"))
                {
                    var last = Version(p.documentId);
                    if (Data.Text(last, "hash") == hash && (kind == "edit" || Data.Text(last, "kind") == "save"))
                    {
                        Prune(meta);
                        return new SaveResult { project = p, document = Data.As<Document>(meta), version = Data.As<RibbonStudio.Version>(last) };
                    }
                }
                string folder = Path.Combine(Folder(p.documentId), "versions");
                Directory.CreateDirectory(folder);
                int seq = Convert.ToInt32(Data.Get(meta, "latestSequence") ?? Data.Get(meta, "count") ?? 0) + 1;
                var record = new Dictionary<string, object> { { "id", Guid.NewGuid().ToString() }, { "sequence", seq }, { "createdAt", Data.Now() }, { "kind", kind }, { "note", note }, { "hash", hash }, { "project", p }, { "valid", ProjectRules.Validate(p).valid } };
                if (artifacts != null)
                    record["artifacts"] = artifacts;
                Data.AtomicJson(Path.Combine(folder, Data.Text(record, "id") + ".json"), record);
                meta["label"] = p.origin == null ? p.@namespace : p.origin.fileName;
                meta["updatedAt"] = record["createdAt"];
                meta["lastVersion"] = record["id"];
                meta["latestSequence"] = seq;
                Prune(meta);
                return new SaveResult { project = p, document = Data.As<Document>(meta), version = Data.As<RibbonStudio.Version>(record) };
            }
        }
        public void Bind(string id, Binding binding)
        {
            lock (gate)
            {
                var meta = Get(id);
                var list = Data.Items(Data.Get(meta, "bindings")).Select(Data.As<Binding>).Where(b => !Data.Same(b.monPath, binding.monPath) || !String.Equals(b.manifestName, binding.manifestName, StringComparison.OrdinalIgnoreCase) || b.entryName != binding.entryName).ToList();
                list.Add(binding);
                meta["bindings"] = list;
                if (String.IsNullOrEmpty(Data.Text(meta, "sourcePath")))
                    meta["sourcePath"] = binding.filePath;
                Data.AtomicJson(Path.Combine(Folder(id), "document.json"), meta);
            }
        }
        public List<Binding> Bindings(string id)
        {
            return Data.Items(Data.Get(Get(id), "bindings")).Select(Data.As<Binding>).ToList();
        }
    }
}
