# 打开开发参考

完整 API、源码阅读、WPF 静态调用关系、测试目录和源码档案保存在同修订的 `Phayton-0.3.4.2-Developer-Reference.zip` 中。

1. 手工取得与应用配套的开发参考 ZIP，并解压到你选择的本地目录。
2. 在工作台选择“开发参考”，再选择包含 `developer-reference.json` 的解压目录。
3. 工作台核对清单、版本、源码修订和文件摘要后，以本机只读方式提供内容。

没有选择开发参考时，任务指南和应用功能仍可使用。选择错误版本、文件被改动或目录不可用时，核对提示并重新选择；本页不会后台下载或写入设置。

## 配套开发包

<button id="reference-package-information" type="button">查看配套包信息</button>
<dl>
<dt>ZIP 文件</dt><dd><code id="reference-package-name">点击后读取本机应用清单</code></dd>
<dt>完整 ZIP SHA256</dt><dd><code id="reference-package-sha">尚未读取</code></dd>
<dt>开发清单 SHA256</dt><dd><code id="reference-manifest-sha">尚未读取</code></dd>
</dl>
<p id="reference-package-status" role="status" aria-live="polite">此按钮只读取本机配套包信息，不下载开发包。</p>

## 打开页面

<p>准备打开：<code id="reference-target">index.html</code></p>
<button id="open-reference" type="button">检查并打开已选择的开发参考</button>
<p id="reference-status" role="status" aria-live="polite">选择完成后，点击按钮检查当前页面是否可用。</p>

<script type="module" src="resources/developer-reference.mjs"></script>
