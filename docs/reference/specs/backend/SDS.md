# 后端 SDS · 接口与实现

[!INCLUDE [实现接点](../../../../artifacts/docfx/navigation/specifications/ports.md)]

ConfigSnapshot=value/revision/schema/file；ConfigCommit=新修订/变更/生效说明。完整签名见 [Roslyn](../../../../artifacts/docfx/navigation/specifications/bindings.md)。

WorkspaceSettings/LaunchProfile只含数据；UI交草稿与临时密码，AvevaLaunchScript管凭据、目录、进程与日志。留空沿用；取消记住不删除旧凭据。入口已创建即返回PID；后续记录失败仅警告。

提交：expectedRevision→锁→检查→历史/日志→替换。保留未知字段；拒绝未知 schema、重复字段、伪造修订、越界路径。恢复只校验/提交目标，不回写旧凭据、其他配置或历史。

调用核验 PID/启动时间/模块/协议；模型操作由 Host 主线程执行。批处理有序、首错停，无通用回滚。REHASH 仅受控值。

CLI 退出码：参数2、Host不支持6、修订冲突10；mayHaveExecuted 表示未知，不以包装成功吞掉业务失败。日志故障单记，秘密脱敏；禁止诱发重复执行。
