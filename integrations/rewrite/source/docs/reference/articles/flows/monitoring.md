# 许可监测：从探测到可信的页面状态

<div class="flow-sequence" aria-label="许可监测流程">

轮询／复查／就绪要求 → 冻结身份 → 分项探测 → 健康归约 → 缓存／订阅 → Dispatcher

</div>

| 环节 | 入口与结果 |
| --- | --- |
| 入口／身份 | <xref:LaunchDesk.Bootstrap.DesktopCompositionRoot> 启动 PollAsync，<xref:LaunchDesk.Desktop.LicensePageViewModel> 主动 RefreshAsync，启动处理器要求 RequireFreshAsync。<xref:LaunchDesk.UseCases.ResourceSupervisor> 按 <xref:LaunchDesk.Kernel.ResourceDescriptor> 的 IdentityKey 隔离缓存与并发门。 |
| 探测／归约 | <xref:LaunchDesk.Windows.WindowsResourceProbe> 按描述采集 RMS／RULES／服务／VM；超时或身份不符算失败，不拼接其他资源的成功。<xref:LaunchDesk.Kernel.ConnectionHealthPolicy> 结合轮次、连续失败、恢复和时间归约。WatchAsync 先发已有快照，再发新轮次；页面拒收旧修订／端点的迟到证据。 |

RequireFresh 只接受身份匹配且未过期的就绪证据，否则重探冻结描述，不替换为同 ID 的另一版本。单项 VM 失败不等于整个许可丢失；端口、VM 可见均不证明系统／应用就绪或业务授权。快照仅在内存，重启重探，不经协调器或后台日志；取消订阅／轮询不修改 VM、许可或外部应用。场景：AR06–08、F04，不替代真实登录验证。[公共协议](index.md)
