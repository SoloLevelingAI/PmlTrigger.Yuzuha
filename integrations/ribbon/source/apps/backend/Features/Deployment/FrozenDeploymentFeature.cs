using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
namespace RibbonStudio.Backend
{
    // v1 remains compatible. v2 owns the complete input rather than accepting it again at apply.
    public sealed class FrozenDeploymentFeature
    {
        sealed class Entry
        {
            public string Kind, Digest, Version, Document, InputJson, PlanJson;
            public DateTime Expires;
            public object Result;
        }
        readonly WorkspaceFiles store;
        readonly DeploymentFeature deployment;
        readonly Dictionary<string, Entry> plans = new Dictionary<string, Entry>();
        readonly Func<DateTime> now;
        public FrozenDeploymentFeature(WorkspaceFiles files, DeploymentFeature feature) : this(files, feature, () => DateTime.UtcNow) { }
        public FrozenDeploymentFeature(WorkspaceFiles files, DeploymentFeature feature, Func<DateTime> clock)
        { store = files; deployment = feature; now = clock; }
        static void Fields(object input, params string[] required)
        {
            var map = input as Dictionary<string, object>;
            if (map == null || map.Keys.Except(required).Any() || required.Any(k => !map.ContainsKey(k) || map[k] == null))
                throw new ApiError(422, "v2 请求字段不正确");
        }
        void Prune()
        { foreach (var id in plans.Where(p => p.Value.Expires <= now()).Select(p => p.Key).ToList()) plans.Remove(id); }
        object Target(string kind, object input)
        {
            var p = Data.As<Project>(Data.Get(input, "project"));
            if (kind == "file-update") return deployment.FilePreview(p);
            // Explicit selections may not be covered by discovery. Only persisted bindings grant
            // management authority; a caller's preferredBinding cannot adopt native installation files.
            var installation = Manifests.Inspect(Data.Text(input, "monPath"));
            var source = Data.Text(store.Get(p.documentId), "sourcePath");
            if (Manifests.Inside(source, installation.directory) && !store.Bindings(p.documentId).Any(b => Data.Same(b.filePath, source) && Data.Same(b.monPath, installation.monPath)))
                throw new ApiError(403, "所选安装目录内未托管的原生 UIC 只读");
            return deployment.Registration(input, false);
        }
        static string Digest(object value) { return Data.Hash(Data.CanonicalJson(value)); }
        public object Preview(object input, string kind)
        {
            lock (Data.WriteGate)
            {
                Fields(input, kind == "file-update" ? new[] { "project" } : new[] { "project", "monPath", "manifestName", "entryName" });
                var p = ProjectRules.Normalize(Data.As<Project>(Data.Get(input, "project")));
                ProjectRules.Require(p);
                if (String.IsNullOrEmpty(p.documentId)) throw new ApiError(422, "v2 请先保存或导入文档");
                var data = Data.Map(Json.Read<object>(Json.Write(input))); data["project"] = p;
                var inputJson = Json.Write(data);
                Prune();
                if (Encoding.UTF8.GetByteCount(inputJson) > 4 * 1024 * 1024) throw new ApiError(422, "v2 计划输入超过 4 MiB");
                if (plans.Count >= 64 || plans.Values.Sum(e => (long)Encoding.UTF8.GetByteCount(e.InputJson) + Encoding.UTF8.GetByteCount(e.PlanJson)) + Encoding.UTF8.GetByteCount(inputJson) > 32L * 1024 * 1024)
                    throw new ApiError(423, "v2 计划缓存已满，请等待过期后重新预览");
                var target = Target(kind, data);
                var id = Guid.NewGuid().ToString(); var expires = now().AddMinutes(5);
                var version = Data.Text(store.Get(p.documentId), "lastVersion");
                var plan = new Dictionary<string, object> {
                    { "contractVersion", 2 }, { "planId", id }, { "kind", kind }, { "documentId", p.documentId },
                    { "expectedVersionId", version }, { "projectDigest", Digest(p) },
                    { "exportDigest", Data.Hash(Encoding.UTF8.GetBytes(UicFiles.Export(p))) },
                    { "expiresAt", expires.ToString("o") }, { "targetPlan", target }
                };
                var digest = Digest(plan); plan["planDigest"] = digest;
                var planJson = Json.Write(plan);
                if (plans.Values.Sum(e => (long)Encoding.UTF8.GetByteCount(e.InputJson) + Encoding.UTF8.GetByteCount(e.PlanJson)) + Encoding.UTF8.GetByteCount(inputJson) + Encoding.UTF8.GetByteCount(planJson) > 32L * 1024 * 1024)
                    throw new ApiError(423, "v2 计划缓存已满");
                plans.Add(id, new Entry { Kind = kind, Digest = digest, Document = p.documentId, Version = version, InputJson = inputJson, PlanJson = planJson, Expires = expires });
                return Json.Read<object>(planJson);
            }
        }
        public object Apply(object input, string kind)
        {
            lock (Data.WriteGate)
            {
                Fields(input, "planId", "planDigest");
                Guid parsed;
                if (!Guid.TryParseExact(Data.Text(input, "planId"), "D", out parsed) || !System.Text.RegularExpressions.Regex.IsMatch(Data.Text(input, "planDigest"), "^[a-f0-9]{64}$"))
                    throw new ApiError(422, "v2 计划标识或摘要格式无效");
                Prune(); Entry entry;
                if (!plans.TryGetValue(Data.Text(input, "planId"), out entry) || entry.Kind != kind || entry.Digest != Data.Text(input, "planDigest"))
                    throw new ApiError(409, "v2 计划缺失、过期或摘要不符，请重新预览；不确定写入先人工核对");
                if (entry.Result != null) return entry.Result;
                if (Data.Text(store.Get(entry.Document), "lastVersion") != entry.Version) throw new ApiError(409, "文档修订已变化，请重新预览");
                var data = Data.Map(Json.Read<object>(entry.InputJson));
                var frozen = Data.Map(Json.Read<object>(entry.PlanJson));
                var p = Data.As<Project>(data["project"]);
                if (Digest(p) != Data.Text(frozen, "projectDigest") || Data.Hash(Encoding.UTF8.GetBytes(UicFiles.Export(p))) != Data.Text(frozen, "exportDigest") || Digest(Target(kind, data)) != Digest(frozen["targetPlan"]))
                    throw new ApiError(409, "部署目标或导出内容已变化，请重新预览");
                var target = frozen["targetPlan"];
                data["expectedUicHash"] = Data.Get(target, "uicHash");
                if (kind != "file-update") data["expectedManifestHash"] = Data.Get(target, "manifestHash");
                var receipt = new Dictionary<string, object> {
                    { "contractVersion", 2 }, { "planId", Data.Text(input, "planId") }, { "planDigest", entry.Digest },
                    { "state", "unknown" }, { "workspace", "unknown" }, { "deployment", "unknown" },
                    { "activation", new { reloadRequired = true, runtimeVerified = false } },
                    { "recoveryActions", new[] { "核对原工作区历史、UIC/选定 XML 和备份；不要自动重放" } }
                };
                // Consume before entering the legacy write engine, including failures after a partial write.
                entry.Result = receipt;
                try
                {
                    var result = kind == "file-update" ? deployment.FileApply(data, true) : deployment.Registration(data, true, true);
                    var uic = Data.Read(Data.Text(target, "uicPath"));
                    var expectedUic = Data.Text(target, kind == "file-update" ? "nextUicHash" : "uicHash");
                    if (uic == null || Data.Hash(uic) != expectedUic) throw new IOException("UIC 写后核验失败");
                    if (kind != "file-update")
                    {
                        var manifest = Data.Read(Data.Text(target, "manifestPath"));
                        if (manifest == null || Data.Hash(manifest) != Data.Text(target, "nextManifestHash")) throw new IOException("清单写后核验失败");
                    }
                    bool unchanged = Convert.ToBoolean(Data.Get(result, "unchanged"));
                    receipt["state"] = receipt["deployment"] = unchanged ? "unchanged" : "deployed";
                    receipt["workspace"] = "saved";
                    receipt["result"] = new { uicPath = Data.Text(result, "uicPath"), manifestPath = Data.Text(result, "manifestPath"), nextUicHash = Data.Text(result, "nextUicHash"), nextManifestHash = Data.Text(result, "nextManifestHash"), backupDirectory = Data.Text(result, "backupDirectory"), backup = Data.Text(result, "backup") };
                    receipt["recoveryActions"] = new string[0];
                }
                catch (Exception error) { receipt["code"] = "RIBBON_RESULT_UNKNOWN"; receipt["diagnosticType"] = error.GetType().Name; receipt["message"] = "写入引擎未产生可确认回执，请人工核对；同计划不会再次执行"; }
                return receipt;
            }
        }
    }
}
