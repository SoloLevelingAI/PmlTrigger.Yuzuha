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
        internal CafEntryDescription(bool started) { Started = started; }
    }

    // Pure lifecycle state machine; isolated tests compile this original source
    // without vendor dependencies. No factory, process, file, registry or network calls.
    internal sealed class CafLifecycle
    {
        private readonly object gate = new object();
        private bool started;
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
        internal void Stop() { lock (gate) { started = false; } }
        internal CafEntryDescription Describe() { lock (gate) { return new CafEntryDescription(started); } }
    }
}
