# Database Feature 交接 · 2026-10-31

## 来源与唯一实现

映射上层 4.1 / 4.2，4.3 尚未实施。来源为平台 design 第 5 节、application-database-boundary/spec.md 和现有 Knowledge 使用的 Microsoft.Data.Sqlite 10.0.0 / SQLitePCLRaw.bundle_e_sqlite3 2.1.13。复用相同供应商版本，没有复制知识库引擎、Workspace JSON 事务或启动协调器。无相邻仓库数据搬运。

新增 `src/PmlTwin.Database.Contracts` 和 `src/PmlTwin.Database`，交付独立 net10 DLL。公开端口不暴露 Sqlite 类型；实现只访问调用方明确指定的应用数据库，不依赖 AVEVA/WPF/安装器。全部公开 API 有中文 XML 文档并启用 CS1591 为错误。

## 已实现

- 固定 DataScope 的配置/文档文本修订会话；完整历史读取、SHA-256 校验、单对象及原子批次 ExpectedRevision 比较交换。
- 独立连接写竞争由 SQLite BEGIN IMMEDIATE 序列化；后段冲突或 SQLite 异常全批回滚。
- 显式只读会话、格式版本门禁、每次操作复验、新版本/外来库拒绝、进程中断未提交事务由 SQLite 恢复。
- 独立测试项目和真实子进程夹具；测试及依赖边界细节见 `docs/reference/database.md`。

## 验证

使用 `C:/Users/26648/.dotnet/dotnet.exe` SDK 10.0.302，2026-10-31 执行：

```powershell
& C:/Users/26648/.dotnet/dotnet.exe restore tests/PmlTwin.Database.Tests/PmlTwin.Database.Tests.csproj --disable-parallel -m:1 --verbosity minimal
& C:/Users/26648/.dotnet/dotnet.exe test tests/PmlTwin.Database.Tests/PmlTwin.Database.Tests.csproj --no-restore -m:1 --verbosity minimal
& C:/Users/26648/.dotnet/dotnet.exe test tests/PmlTwin.Database.Tests/PmlTwin.Database.Tests.csproj --no-build --no-restore -m:1 --logger 'console;verbosity=normal'
& C:/Users/26648/.dotnet/dotnet.exe test tests/PmlTwin.Database.Tests/PmlTwin.Database.Tests.csproj --no-restore -m:1 --logger 'console;verbosity=normal'
```

结果：两个独立 DLL、进程夹具与测试均构建成功；初轮 10/10 通过（1.918 秒）。独立审查发现仅靠 user_version 可接管格式 1 外来数据库，已增加 application_id 与完整列/类型/非空/组合主键门禁，并在每次打开及提交事务内复验。最终第四条命令经审批在沙箱外构建及测试，14/14 通过，总时间 4.5144 秒。覆盖项：跨连接并发、后段冲突回滚、SQLite 触发器失败回滚、四维 scope 隔离/历史、只读、新版本原字节保留、格式 1 外来库/缺列/错误主键拒绝、空格式 0 拒绝与显式恢复边界、哈希篡改、子进程提交/kill 未提交事务恢复、程序集边界。

首次构建被 xUnit2031 分析器拒绝，已改为 Assert.Single 的过滤谓词重载后构建通过。沙箱 NuGet 审计网络失败时通过审批重新执行 restore，未禁用审计。沙箱中测试运行器启动后无输出，已中断；最终第三条命令经审批在沙箱外执行成功。恢复夹具只创建/删除测试临时目录中的数据库，不触及活动数据。

Debug/net10.0 程序集 SHA-256（源码非发布身份，后续重新构建需重取）：

| 文件 | SHA-256 |
|---|---|
| PmlTwin.Database.Contracts.dll | `6E56DB75214857CB72AAA26AF675FB755D2C3A5D88C1F8B5B5F0AEB6114522B8` |
| PmlTwin.Database.dll | `3293CB123E04D4D426D667CC0E660695E2D2057C0819056CAD31985DEA36AA6A` |

## 接线与剩余

4.1 的独立 DLL 和契约最小接口已落源；主代理已接开发 solution、文档索引、DocFX及项目依赖图，架构21项通过。产品装配注入与发行包仍未切换，不声明产品包集成完成。

4.2 的配置/文档修订存储切片按任务文字验收完成；现有 UI/CLI 仍用原权威存储，未宣称完成业务迁移。唯一写者规则见 reference 文档。主代理仅将存储切片标完成，显式切换、回退、只读导入和真实调用方迁移仍归 4.4/4.5，不扩张完成范围。

4.3 未实现。现有启动 OperationStore 保持事实所有者。建议下一片为它抽取 IOperationFactRepository：按 operationId + inputDigest 接受事实、expected phase/version 更新、唯一 attempt 约束、unknown 读回；以 adapter 替换原存储，不增加第二套协调者。大型资产另设 IAssetIndex：scope/相对受限根路径/size/SHA-256/phase，先暂存与验证，再写索引；中断阶段分别恢复。以上仅为后续端口责任建议，不是已生效接口。

未实现 SQLite 在线备份/迁移、磁盘不足夹具、WAL 一致备份、知识库生命周期接线、Ribbon HTTP adapter、工程数据库访问。未提交、推送或归档规范变更。
