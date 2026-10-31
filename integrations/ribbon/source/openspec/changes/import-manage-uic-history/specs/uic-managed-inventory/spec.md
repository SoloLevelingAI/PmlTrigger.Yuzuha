# Spec Delta

## Purpose

提供可筛选的模块加载条目管理界面，让用户区分明确托管、既有自定义及保护条目，使用可核验的所有权记录决定可修改范围，并按原路径维护已有 UIC 而避免重复部署。

## ADDED Requirements

### Requirement: 可筛选条目表

系统 SHALL 显示名称、路径、来源、权限、文件状态及加载状态，并按名称/路径、来源和加载状态筛选。

#### Scenario: 查找 Ytools
- **WHEN** 用户在 Marine 模块清单中搜索 Ytools
- **THEN** 表格 SHALL 显示其实际绝对路径和未托管/托管状态

### Requirement: 保护与明确接管

系统 SHALL 保护安装内既有条目与未识别来源，明确说明保守分类不等同厂商签名鉴定。自定义 UIC SHALL 可直接编辑并在确认部署时备份覆盖；覆盖成功后或明确接管后才授予加载管理权限。名称前缀不能单独授予权限。

#### Scenario: 尝试移除原生条目
- **WHEN** 客户端请求移除保护条目
- **THEN** 后端 SHALL 拒绝，清单 SHALL 不变

### Requirement: 原路径更新

本机路径导入文件的更新 SHALL 以持久化记录中的 UIC 路径为单位，确认后先备份再覆盖一次，不要求选择任何模块 XML，不需要事先接管，不新增哈希副本。系统 SHALL 收集已发现安装及已知绑定中的全部模块引用，显示可筛选的模块表与扫描失败位置。所有已有 XML SHALL 保持原字节；没有找到模块引用时仍允许更新自定义 UIC。

#### Scenario: 多模块共用文件
- **WHEN** Design、Draft 等多个模块引用同一 UIC，用户确认更新
- **THEN** 系统 SHALL 备份并覆盖该文件一次，保存前后版本与关联信息，所有模块 XML SHALL 保持原字节

### Requirement: 加入模块独立于更新文件

已有 UIC 加入模块 SHALL 单独选择安装和模块，只修改所选模块 XML 的加载登记，备份清单并保存记录，UIC 内容和其他模块 XML SHALL 保持原字节。已有相同登记 SHALL 不重复添加；过期预览 SHALL 拒绝写入。

系统 SHALL 在顶部工具栏提供“加入模块”独立入口，部署窗口 SHALL 显示“更新 UIC / 加入模块”页签，加入表单 SHALL 不藏在折叠区域。

加入模块 SHALL 使用穿梭框：左侧未关联、右侧已关联及本次待加入，显示数量、独立搜索、全选及左右移动。已有关联 SHALL 根据所选安装的实际 XML 收集并标记；已有条目不可在此移除，右移为待加入，左移仅取消本次选择。无法读取的 XML SHALL 明示并禁选。批量预览 SHALL 列出全部待加入模块，确认后逐项备份登记；部分失败 SHALL 明示已成功数量并保留其余待加入项，不宣称批量事务成功。

#### Scenario: 清晰查看本次选择
- **WHEN** 用户勾选左侧两个模块并右移
- **THEN** 右侧 SHALL 显示两个待加入项和已有加载项，移动前后 XML SHALL 不变，左移待加入项 SHALL 仅取消该选择

### Requirement: 可核对的加载变更预览

批量预览 SHALL 进入独立确认步骤，显示每份实际 XML 路径、修改前后登记数量、Name/Path 对照与高亮新增登记，并提供完整修改前和修改后 XML。内容 SHALL 来自后端解析实际文件与最终补丁，不得只重复模块名单。用户 SHALL 可返回调整选择；修改后 XML SHALL 与确认写入的内容一致，过期摘要 SHALL 阻止写入。

#### Scenario: 核对实际新增登记
- **WHEN** 用户预览将 UIC 加入未关联模块
- **THEN** 页面 SHALL 显示此模块原先未关联、将新增的完整 CustomizationFile 登记、原有加载数以及最终完整 XML，预览阶段源文件 SHALL 不变

#### Scenario: 将共享 UIC 加入 Paragon
- **WHEN** 用户预览并确认将现有 UIC 加入 Paragon
- **THEN** 只有 Paragon 的清单 SHALL 增加登记，UIC 内容 SHALL 不变

#### Scenario: 修改后接管 Ytools
- **WHEN** 导入并编辑的 YtoolsAM 被接管并部署
- **THEN** 原修改 SHALL 保留，Name=Ytools 的 Path SHALL 保持原绝对路径

#### Scenario: 导入后直接覆盖
- **WHEN** 用户导入并修改自定义 UIC，未执行接管而直接确认部署
- **THEN** 系统 SHALL 完整备份旧文件、覆盖原路径、保留原加载登记，并在成功后记录为托管

### Requirement: 可恢复移除加载

系统 SHALL 只允许托管条目移除/恢复加载，保存清单前后快照，保留 UIC 文件与历史，并拒绝过期清单摘要。

#### Scenario: 恢复已移除的加载项
- **WHEN** 用户恢复一个已移除的托管加载项
- **THEN** 清单 SHALL 恢复单个原加载登记，其他条目和 UIC 文件 SHALL 保留
