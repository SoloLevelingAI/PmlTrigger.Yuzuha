# 安装到工程操作：共享契约与ION原生设计

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
