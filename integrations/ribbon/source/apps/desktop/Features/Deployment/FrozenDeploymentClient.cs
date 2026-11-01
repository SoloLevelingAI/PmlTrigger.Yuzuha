using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Text;
namespace RibbonStudio
{
    public sealed class FrozenPlan
    {
        public int contractVersion { get; set; }
        public string planId { get; set; } public string planDigest { get; set; }
        public string expectedVersionId { get; set; } public string expiresAt { get; set; }
        public string kind { get; set; } public Plan targetPlan { get; set; }
    }
    public sealed class FrozenActivation { public bool? reloadRequired { get; set; } public bool? runtimeVerified { get; set; } }
    public sealed class FrozenResult { public string backupDirectory { get; set; } public string backup { get; set; } }
    public sealed class FrozenReceipt
    {
        public int contractVersion { get; set; } public string planId { get; set; } public string planDigest { get; set; }
        public string state { get; set; } public FrozenActivation activation { get; set; } public FrozenResult result { get; set; }
    }
    /// <summary>UI-only v2 transport; unknown locks this tool and preserves the original plan identity.</summary>
    public sealed class FrozenDeploymentClient
    {
        readonly ApiClient api; readonly HashSet<string> consumed = new HashSet<string>();
        static string unknownPlanId;
        static string StatePath { get { return Path.Combine(Environment.GetEnvironmentVariable("UIC_UI_STATE_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RibbonStudio"), "pending-deployment.json"); } }
        public string UnknownPlanId
        {
            get
            {
                try { if (File.Exists(StatePath)) return Json.Read<FrozenPlan>(File.ReadAllText(StatePath)).planId ?? "pending-record-unreadable"; }
                catch { return "pending-record-unreadable"; }
                return unknownPlanId;
            }
        }
        public bool Locked { get { return UnknownPlanId != null; } }
        public const string ActivationMessage = "需重新进入对应模块或重启 AVEVA 后检查；实际 Ribbon 加载未核验。";
        public FrozenDeploymentClient(ApiClient client) { api = client; }
        void Available() { if (Locked) throw new Exception(UnknownMessage(UnknownPlanId)); }
        public void EnsureAvailable() { Available(); }
        public string Status { get { return Locked ? UnknownMessage(UnknownPlanId) : ActivationMessage; } }
        static string UnknownMessage(string id) { return "结果不确定 · 计划 " + id + "。请核对工作区历史、目标文件与备份；不要重试或重新预览。所有部署页已暂停写入，关闭页面不会解除。"; }
        public static bool ConfirmManualCheck(Window owner)
        {
            var id = new FrozenDeploymentClient(null).UnknownPlanId;
            if (id == null) return false;
            if (MessageBox.Show(owner, UnknownMessage(id) + "\n\n是否已经人工核对上述文件、历史和备份？确认仅解除写入暂停，不代表 Ribbon 已加载。", "人工核对结果", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return false;
            AcknowledgeManualCheck(id); return true;
        }
        static FileStream Lease()
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(StatePath)); return new FileStream(StatePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch { throw new Exception("另一部署仍在执行或状态存储不可用；未发送写入请求，也未解除暂停。"); }
        }
        static void DeletePending() { if (File.Exists(StatePath)) File.Delete(StatePath); if (File.Exists(StatePath)) throw new Exception("无法清除部署暂停状态"); unknownPlanId = null; }
        public static void AcknowledgeManualCheck(string expectedId = null)
        {
            using (var lease = Lease())
            {
                if (expectedId != null && new FrozenDeploymentClient(null).UnknownPlanId != expectedId) throw new Exception("待核对计划已变化，请重新核对编号");
                DeletePending();
            }
        }
        void Pending(string id, string digest, string kind)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StatePath));
                var bytes = Encoding.UTF8.GetBytes(Json.Write(new { planId = id, planDigest = digest, kind, endpoint = new Uri(api.Address).GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped) }));
                using (var stream = new FileStream(StatePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            }
            catch { throw new Exception("无法持久记录部署计划；未发送写入请求。请检查部署暂停状态和存储权限。"); }
        }
        void ClearPending() { DeletePending(); }
        static object Value(object value, string key) { object result; var map = value as Dictionary<string, object>; return map != null && map.TryGetValue(key, out result) ? result : null; }
        public async Task<Plan> ApplyLegacy(object input)
        {
            using (var lease = Lease())
            {
            Available(); var id = "v1-first-deployment-" + Guid.NewGuid(); Pending(id, "legacy-no-frozen-digest", "v1-first-deployment");
            try { var raw = await api.Post<Dictionary<string, object>>("deployment/apply", input); if (!(Value(raw, "unchanged") is bool) || !(Value(raw, "uicPath") is string)) throw new Exception("v1 回执无效"); var result = Json.Read<Plan>(Json.Write(raw)); ClearPending(); return result; }
            catch (Exception) { unknownPlanId = id; throw new Exception(UnknownMessage(id)); }
            }
        }
        public async Task<FrozenPlan> Preview(string kind, object input)
        {
            Available(); var plan = await api.PostV2<FrozenPlan>("deployment/" + kind + "-preview", input);
            if (plan == null || plan.contractVersion != 2 || String.IsNullOrEmpty(plan.planId) || String.IsNullOrEmpty(plan.planDigest) || plan.targetPlan == null || plan.kind != (kind == "file" ? "file-update" : "registration-add")) throw new Exception("冻结预览响应无效，请重新预览");
            return plan;
        }
        public async Task<FrozenReceipt> Apply(string kind, FrozenPlan plan)
        {
            using (var lease = Lease())
            {
            Available(); if (plan == null || !consumed.Add(plan.planId)) throw new Exception("计划已消费，请重新预览；不重放原写入");
            Pending(plan.planId, plan.planDigest, kind);
            try
            {
                var raw = await api.PostV2<Dictionary<string, object>>("deployment/" + kind + "-apply", new { planId = plan.planId, planDigest = plan.planDigest });
                var activation = Value(raw, "activation");
                if (!(Value(raw, "contractVersion") is int) || (int)Value(raw, "contractVersion") != 2 || !(Value(activation, "runtimeVerified") is bool) || (bool)Value(activation, "runtimeVerified") || !(Value(activation, "reloadRequired") is bool)) throw new Exception("写入回执字段类型不确定");
                var receipt = Json.Read<FrozenReceipt>(Json.Write(raw));
                if (receipt == null || receipt.contractVersion != 2 || receipt.planId != plan.planId || receipt.planDigest != plan.planDigest || (receipt.state != "deployed" && receipt.state != "unchanged") || receipt.activation == null || receipt.activation.runtimeVerified != false || !receipt.activation.reloadRequired.HasValue) throw new Exception("写入回执不确定");
                ClearPending(); return receipt;
            }
            catch (ApiRequestRejected error) { if (error.Status < 500 && error.Status != 408) { ClearPending(); throw; } unknownPlanId = plan.planId; throw new Exception(UnknownMessage(UnknownPlanId)); }
            catch (Exception) { unknownPlanId = plan.planId; throw new Exception(UnknownMessage(UnknownPlanId)); }
            }
        }
    }
}
