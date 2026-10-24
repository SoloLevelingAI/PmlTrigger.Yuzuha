# 作品集媒体

`Phayton-Workflow-zh-CN.mp4` 是约 2 分 23 秒的中文配音介绍，1920×1080、24 fps、H.264/AAC。字幕已烧录，同时提供 `.srt`。`storyboard.json` 保存全部旁白、场景和证据边界；`video-info.json` 保存时长、章节及文件散列。

`screenshots/desktop-light.png` 与 `desktop-dark.png` 来自本次发布构建的原生屏幕捕获，包含真正的窗外阴影；`knowledge.png` 来自隔离状态下的 WPF 界面回归。`native-window.json` 为窗口检查回执。公开图片不包含真实账号、凭据或用户知识库内容。

脚本加载、失败与修正、版本更新的画面根据 2026-10-23 已提交的 E3D 2.1 验证记录编排，标注为历史实测重放。它们不是新一次 AVEVA 执行的录像。对象/视图协作是能力示意。用户数量来自维护者提供的汇总，不是遥测。

## 重新生成

需要 Windows、Python 3 + Pillow、FFmpeg，以及系统已安装的中文 Huihui OneCore 语音。旁白使用本地 SAPI；脚本不会下载声音或改写系统语音注册表。

先发布桌面并运行 `scripts/Test-WindowFrame.ps1` 与 `scripts/Test-Desktop.ps1`，获取新的原生窗口和页面截图。窗口检查需要可交互桌面，会启动带中性背景的测试窗口；输出目录须为新目录。

```powershell
pwsh -NoProfile -STA -File scripts/Render-PortfolioVoice.ps1 `
  -OutputDirectory artifacts/portfolio/voice
python scripts/render-portfolio-video.py `
  --ffmpeg C:/tools/ffmpeg.exe `
  --frames artifacts/portfolio/frame-release `
  --ui C:/captures/phayton-ui `
  --audio artifacts/portfolio/voice `
  --work artifacts/portfolio/video-build
```

`--ui` 指向 `Test-Desktop.ps1` 的输出目录。渲染器使用仓库自有 PML 示例与公开验证摘要，输出 MP4、SRT、海报和章节信息，最后完整解码检查视频。发布前仍需检查图像、字幕、来源标注与账号脱敏。没有真实工程素材时，应保留历史重放/能力示意标记。
