using System;

namespace PmlTwin.Caf.Adapter
{
    /// <summary>只读入口状态；不含数据库或执行权限。</summary>
    public sealed class CafEntryDescription
    {
        /// <summary>稳定入口身份，不冒充管理器 operation ID。</summary>
        public string Id { get { return "pmltwin.caf.readonly-entry"; } }
        /// <summary>宿主装配是否已开始。</summary>
        public bool Started { get; private set; }
        /// <summary>明确拒绝从 CAF Start 自动运行管理器。</summary>
        public bool AutomaticManagerLaunch { get { return false; } }
        /// <summary>适配器只展示描述，不请求写入或执行权限。</summary>
        public bool ReadOnly { get { return true; } }
        /// <summary>从生命周期快照创建只读入口描述，不启动任何外部组件。</summary>
        internal CafEntryDescription(bool started) { Started = started; }
    }

    // Pure lifecycle state machine; isolated tests compile this original source
    // without vendor dependencies. No factory, process, file, registry or network calls.
    /// <summary>仅保存 CAF 启停状态并验证程序集身份；不访问进程、文件、注册表或网络。</summary>
    internal sealed class CafLifecycle
    {
        private readonly object gate = new object();
        private bool started;
        /// <summary>确认 CAF 版本、受支持的公钥令牌和服务管理器存在后，将入口标为已启动。</summary>
        internal void Start(string expectedVersion, string observedVersion, string publicKeyToken, bool hasServices)
        {
            lock (gate)
            {
                if (observedVersion != expectedVersion || publicKeyToken != "17c64733a9775004")
                    throw new NotSupportedException("CAF identity is not supported by this adapter profile.");
                if (!hasServices) throw new ArgumentNullException("serviceManager");
                started = true;
            }
        }
        /// <summary>在锁内清除启动标记，不触发管理器或宿主操作。</summary>
        internal void Stop() { lock (gate) { started = false; } }
        /// <summary>在锁内读取当前启动标记并返回独立的只读描述。</summary>
        internal CafEntryDescription Describe() { lock (gate) { return new CafEntryDescription(started); } }
    }
}
