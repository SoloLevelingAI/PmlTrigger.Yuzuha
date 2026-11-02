# 安装到工程操作：共享契约与ION原生设计

## 紧凑准备工作屏

用户指出实际页面过长、需要减少滚动后，新增独立的[紧凑FigCraft工作屏](compact-preparation.figcraft)。原八阶段总览、六套旧导图和旧回执保留；新稿的四个入口是[1280×720预览](compact-preview-1280.png)、[1440×900预览](compact-preview-1440.png)、[同区详情](compact-details-1280.png)和[未知结果的恢复出口](compact-unknown-1280.png)。所有屏幕均标明“设计示例，未执行”，没有读取或展示本机ENV、秘密、真实计划或部署回执。

新稿保留46px应用标题栏与80px全局功能导航，准备页隐藏工程二级侧栏，八阶段改为横向导航。工作头不超过64px，输入与计划变化左右同屏，底部作用、结果状态和唯一主动作常驻。已有冻结计划的预览态按钮为“重新生成预览”，不会隐式推进或应用；无计划时为“生成预览”。详情替换右侧计划区，契约、脚本、证据和历史按需切换，不向下追加。七个契约字段各有独立原生可编辑状态，逐字消费真实Manager契约；13个工作屏状态之间保留101条原生导航。未知结果保留原回执编号/完整SHA的输入与查看区域，只提供原回执核对和恢复出口，不自动应用、恢复或重放。

[生成器](../../../scripts/build-compact-preparation-design.mjs)调用真实FigCraft CLI 0.6.0，以原生形状、文字、组件、实例及共享变量生成新文件。独立[回执](compact-preparation.receipt.json)绑定生成器、契约、主题、原生文件和四张导图SHA；回读校验1287节点、0图片层、4组件、27实例、22变量的6个模式、完整七字段、101条导航及实际原生输入/结果/原恢复回执/CTA首屏几何。额外详情状态保留在原生文件中，四张PNG只是预览，不替代可编辑设计源。

```powershell
$env:FIGCRAFT_CLI = 'C:/Users/huang y t/AppData/Local/Programs/FigCraft/versions/0.6.0/figcraft-cli.exe'
node scripts/build-compact-preparation-design.mjs --output '<全新绝对目录>'
node scripts/build-compact-preparation-design.mjs --check
```

生成拒绝已有输出目录；原生文件通过`run → convert → export`链实际构建、回读与导图。`--check`只验证已提交文件/来源/导图，不宣称CI重新运行了FigCraft。当前为代理目检，等待用户评价；真实WPF落地、安装、ENV操作和用户视觉认可分别验收。普通导航、阅读详情和主题切换不改变草稿、冻结身份或业务授权。

## 八阶段总览参考（保留历史）

本目录是从真实`ManagerContractCatalog`导出内容与`ion/themes.json`生成的独立可编辑FigCraft参考板。它展示安装交接、选择环境、旧所有权、预览、应用、恢复、启动及核验八阶段，完整七字段和脚本解释位于[全面契约](../../reference/install-to-operation.zh-CN.md)。

- `install-to-operation.figcraft`：8个原生组件、40个原生实例、22个共享颜色变量的6个模式、318个可编辑文字节点；没有截图贴图替代组件。
- `lime.png`至`graphite.png`：六套实际原生渲染预览。
- `receipt.json`：真实生成、内容摘要、原生校验及范围；静态设计不是WPF执行或实机验收。
- `wpf-*.png`：实际原生WPF截图，使用最终发布程序与明确标注的合成回执；包含浅色、小屏深色及未知结果恢复出口。

共享源为`docs/presentation/contract-style-lab/ion/themes.json`。Web CSS、WPF XAML和本板分别消费同一颜色定义；透明色在WPF中显式转换到ARGB，设计板保留变量和实例结构。WPF的原生输入、状态和操作按钮由实际控件实现，不能从设计中的箭头推定运行进度。

生成入口：`node scripts/build-install-journey-design.mjs --output <新的绝对路径>`。生成器拒绝覆盖已有目录，调用已安装FigCraft CLI，转换原生JSON核对组件/实例/变量/原文，再逐板导出。原有设计稿保留；更新此目录前应先目检新目录的六套预览。

本轮代理已目检浅色及深色板：阶段顺序、标题、输入/算法与脚本身份层次清晰，文字无截断，边距与两列对齐。用户视觉认可单独记录，不能由生成成功替代。

三层视觉语义与 Web/WPF 对齐：primary 保留功能标题、操作与结果的主层级；supporting 使用可读的中性小徽标和完整技术身份；ambient 只容纳重复的装饰阶段号。卡片标题不再与编号拼成同一大字，脚本身份置于辅助容器，重复阶段号以低透明背景呈现；正文和唯一信息不降低透明度。原222个文字节点增至318，是8阶段×6模式各增加独立徽标及装饰号后、保留原文字得到的变化。六套调色板源没有改色。

`node tests/ion-visual-hierarchy-browser.mjs` 对真实只读 Web ION 运行六主题正文对比度（至少4.5:1）、原契约文字、统计保留及装饰 `aria-hidden` 检查，并生成1440桌面与390移动截图到 `artifacts/ion-visual-hierarchy`。实际原生板逐项核对六模式的标题、输入、方法和完整脚本身份，318个文字节点均保持可编辑；生成器摘要也写入回执。

`node scripts/build-install-journey-design.mjs --check` 在CI中核对已提交原生文件、共享契约/主题摘要和六张导出尺寸；它不假装在无FigCraft的CI机器上重新执行设计工具。
