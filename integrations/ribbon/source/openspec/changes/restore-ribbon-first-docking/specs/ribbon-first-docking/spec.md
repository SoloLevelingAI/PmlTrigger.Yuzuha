## ADDED Requirements

### Requirement: Ribbon 固定主视角
窗口 SHALL 保持顶部完整命令 Ribbon、UIC 文件标签、左结构树、中央 Ribbon 预览和右侧属性。辅助工具 SHALL 不因切换而替换当前 Ribbon 主视图。

#### Scenario: 切换辅助任务
- **WHEN** 用户查看登记或部署停靠页
- **THEN** SHALL 仍看见当前 UIC 的 Ribbon 及选中按钮属性，顶层 SHALL 不增加对应整页工具标签

### Requirement: 少量停靠页
辅助区 SHALL 由按钮 / 查找、UIC ↔ XML 和部署三个常用停靠页构成。查找 SHALL 使用范围选择合并到对照页；跨文件范围 SHALL 默认只读定位，不隐式批量修改其他文件。

#### Scenario: 查找其他 UIC 的按钮
- **WHEN** 对照范围切换为已打开 UIC
- **THEN** SHALL 展示来源并只读定位，显式选择结果后激活已有文件，而非打开新的功能库主页面

### Requirement: 按需扩展而不丢失主视图
停靠区 SHALL 可收起、调高或拖出，扩展时 SHALL 保留 Ribbon 的最小可见区域。布局恢复和浮动能力 SHALL 沿用已有前端能力，易误触的实际写入 SHALL 仍需独立明确核对。

#### Scenario: 需要更大对照空间
- **WHEN** 用户放大停靠区或拖出工具
- **THEN** SHALL 保留主窗口内的 Ribbon，停靠预览 SHALL 明确指向工具区而不遮盖整个主视图
