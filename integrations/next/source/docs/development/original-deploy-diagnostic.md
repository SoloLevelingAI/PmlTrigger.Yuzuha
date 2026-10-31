---
document_kind: historical-evidence
implementation_status: historical
reviewed_on: 2026-10-29
uid: original-deploy-diagnostic
---
# 原LaunchDesk工具复制失败：只读调查

> 历史记录：保留当时结果，不代表当前进程、机器或许可状态。当前交付边界见[状态页](../status.md)。

## ① 实际运行的是哪一版

进程33048运行D:/VirtualMachines/LaunchDesk/LaunchDesk.exe，文件版本2.0.4.0，产品版本2.0.4-original-ui-launchfix；portable.json将数据定位到同目录Data，而不是默认AppData工作空间。通过只读解包和IL核对实际发布程序，未把另一个源码目录当作正在运行的版本。

## ② 这次操作是什么

2026-10-28北京时间15:15:40、15:15:47、15:16:13三次Deploy，来源SeaGend(29).7z，目标C:/AVEVA/PMLLIB/SeaGend，记录根为当前用户Local/SeaGendManager。三次操作目录分别为a330d4…、50c377…、0b7631…。

## ③ 为什么界面没有原因

三份result.json均是Success=false、Error为空字符串、Receipt=null，目录中只有request/result，没有worker.log；对应最新部署收据仍停在前一天。实际发布IL核对到ToolOperations.Execute/Worker只保存Exception.Message，前端ExecuteToolAsync又以result.Error构造IOException；空字符串没有阶段、异常类型、错误码或兜底文本。因此错误原因到达界面前已经丢失，不只是窗口没展示完整。

## ④ 哪些只读检查已过

同一发布引擎的Configure、NormalizeTarget、ValidateStorage和ValidateSources均通过；厂商7zr对同一个压缩包执行完整性测试退出0。未调用真实目标的Apply，没有更改实际PMLLIB文件或原LaunchDesk程序。

## ⑤ 隔离目录能不能复制

从当前发布包提取的同一引擎，在Example/original-deploy-diagnostic/isolated-e1f4650a609c4cc0815a3ba7b9417845/SeaGend完成5228个文件部署，状态Success。这个新目录与实际SeaGend无关，也没有启动E3D、AM或VM。隔离成功不等于实际目标可写，不能据此排除实际文件占用或权限问题。

## ⑥ 哪些原因尚未确定

三次历史失败缺少原始异常/堆栈/阶段记录，当前不能还原到底是文件占用、权限、锁、进程上下文还是其他错误；不得据空Error猜测。隔离复现未产生同一失败，也没有冒险对真实目标重放部署。

## ⑦ 当前怎么处理

如果急用可先手动更新：关闭使用SeaGend的应用，完整备份实际目录，再按发布包结构覆盖；不要先删除整个目录。若Windows报错，保留准确文件路径和原因。用户询问不构成已执行更新，本轮没有更新实际SeaGend或环境变量。

## ⑧ 后续修复应满足什么

原程序需要在worker结果保存阶段、异常类型/HResult、非空摘要、相关日志路径，并在界面提供可定位的原因；不能再只传Message或裸Error。原项目/Rewrite未修改；新项目的快照边界已加入阶段与空消息兜底，原程序修复仍需独立实施。原始证据留在本机Operations与隔离诊断目录，不发布完整用户配置或凭据。
