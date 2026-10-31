using System;
using Aveva.ApplicationFramework;
#if CAF_E3D31
using CafEntryInterface = Aveva.ApplicationFramework.IAddinInjected;
#else
using CafEntryInterface = Aveva.ApplicationFramework.IAddin;
#endif

namespace PmlTwin.Caf.Adapter
{
    /// <summary>真实 CAF IAddin 薄入口。仅发布只读描述，不自动启动管理器或加载现代程序集。</summary>
    public sealed class PmlTwinAddin : CafEntryInterface
    {
        private readonly CafLifecycle lifecycle = new CafLifecycle();
#if CAF_E3D31
        private const string ExpectedVersion = "4.0.0.0";
#elif CAF_AM121
        private const string ExpectedVersion = "1.1.20.0";
#else
        private const string ExpectedVersion = "1.2.2.0";
#endif
        /// <summary>CAF 展示名。</summary>
        public string Name { get { return "PMLTWIN Read-only Entry"; } }
        /// <summary>说明入口权限和生命周期边界。</summary>
        public string Description { get { return "Read-only PMLTWIN entry description. Manager operations require explicit invocation."; } }
        /// <summary>仅核对已装配 CAF 身份并启动内存状态；不调用供应商服务。</summary>
        public void Start(ServiceManager serviceManager)
        {
            StartWithServices(serviceManager != null);
        }
#if CAF_E3D31
        /// <summary>3.1真实注入入口；不解析或调用任何供应商服务。</summary>
        public void Start(IDependencyResolver resolver) { StartWithServices(resolver != null); }
#endif
        private void StartWithServices(bool hasServices)
        {
            var identity = typeof(CafEntryInterface).Assembly.GetName();
            var token = BitConverter.ToString(identity.GetPublicKeyToken()).Replace("-", "").ToLowerInvariant();
            lifecycle.Start(ExpectedVersion, identity.Version.ToString(), token, hasServices);
        }
        /// <summary>幂等清理内存生命周期，不取消管理器已受理操作。</summary>
        public void Stop() { lifecycle.Stop(); }
        /// <summary>返回独立只读快照，不装配进程外管理器。</summary>
        public CafEntryDescription Describe() { return lifecycle.Describe(); }
    }
}
