"""Build the local portfolio and editable FigCraft boards from one narrative file.
Uses the existing FigCraft CLI separately; no product code or package dependency.
"""
from pathlib import Path
import html
import json
import hashlib

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "docs/portfolio"
DATA = json.loads((OUT / "content.json").read_text(encoding="utf-8"))
e = html.escape


def link(label, href, cls=""):
    return f'<a class="{cls}" href="{e(href)}" target="_blank" rel="noopener">{e(label)} <span aria-hidden="true">↗</span></a>'


def visual(s):
    kind = s["kind"]
    if kind == "cover":
        return '''<div class="hero-art" aria-label="从模型到快照再到记录的概念图">
          <span class="art-index">THE LAUNCH SYSTEM / 001</span>
          <div class="orbit orbit-a"></div><div class="orbit orbit-b"></div>
          <div class="stack stack-back"><span>03 / EVIDENCE</span><strong>执行，有据。</strong><small>AttemptId · Record · Log</small></div>
          <div class="stack stack-mid"><span>02 / SNAPSHOT</span><strong>当次，冻结。</strong><small>Immutable input / Revision</small></div>
          <div class="stack stack-front"><span>01 / MODEL</span><strong>信息，归一。</strong><small>One model. Shared backend.</small><i>↗</i></div>
          <div class="art-foot"><span>C# / WINDOWS / FEATURE COMPOSITION</span><span>→</span></div>
        </div>'''
    if kind == "experience":
        return '''<div class="experience-body"><div class="steps">''' + ''.join(
            f'<div class="step"><span>{e(n)}</span><div><h3>{e(t)}</h3><p>{e(d)}</p></div></div>' for n,t,d in s['steps']) + '''</div>
          <div class="workbench"><div class="panel-head"><span class="lamp"></span> 交互叙事 / 非产品窗口 <span class="mono">MODEL → SNAPSHOT</span></div>
          <div class="mock-split"><aside><span class="micro">发现候选</span><div class="candidate">E3D 3.1 <small>目录 / 初始化线索</small></div><p>预填是起点，<br>人工核对才是确认。</p></aside>
          <div class="mock-form"><label for="demo-project">Project / 项目代码</label><input id="demo-project" value="MAR" maxlength="24" autocomplete="off"><div class="static-fields"><span>Module <b>DESIGN</b></span><span>MDB <b>ALL_NO_MDS</b></span></div><p class="micro">示意数据 · 不读写产品配置，不调用系统</p><button class="primary" id="capture-demo">冻结示意快照 <span>↗</span></button><output id="demo-result" aria-live="polite">修改后点击，观察“当前值”与“当次值”。</output></div></div></div></div>'''
    if kind == "model":
        return '''<div class="model-stage"><div class="model-current"><span class="micro">NOW / 当前草稿</span><h3>LaunchModel</h3><div class="code-line">Project <span id="model-current">MAR</span></div><div class="code-line">Revision <span id="model-revision">0</span></div><p>输入持续更新<br>端口原值保留，校验归后端</p></div><div class="freeze-bridge"><span>冻结 →</span><small>版本检查<br>原子持久化<br>必要记录</small></div><div class="model-frozen"><span class="micro">THEN / 执行时刻</span><h3>Snapshot</h3><div class="code-line">Project <span id="model-frozen">尚未冻结</span></div><div class="code-line">AttemptId <span>本次关联</span></div><p>后续编辑不改本次请求<br>秘密不写入模型文件</p></div></div><div class="role-strip">''' + ''.join(f'<div><h3>{e(a)}</h3><p>{e(b)}</p></div>' for a,b in s['roles']) + '</div>'
    if kind == "execution":
        return '<div class="execution-body"><div class="chain" role="group" aria-label="选择调用链步骤">' + ''.join(
            f'<button class="chain-node {"selected" if i==0 else ""}" data-node="{i}" aria-pressed="{"true" if i==0 else "false"}"><span class="node-n">0{i+1}</span><span>{e(n["name"])}<small>{e(n["code"])}</small></span><span class="node-arrow">↗</span></button>' for i,n in enumerate(s['nodes'])) + '''</div><div class="node-detail"><span class="micro">CALL CHAIN / 点击左侧步骤</span><div class="detail-num" id="detail-num">01</div><h3 id="detail-name">点击启动</h3><code id="detail-code">LaunchInteraction</code><p id="detail-text">收集执行事件，调用同一Workspace；前端不决定启动协议。</p><a id="detail-source" target="_blank" rel="noopener" href="''' + e(s['nodes'][0]['source']) + '''">打开该步源码 ↗</a><div class="failure-note">快照 / 必要记录失败 → 不启动<br>创建后状态不确定 → 人工核验，不自动重试</div></div></div>'''
    if kind == "composition":
        return '''<div class="composition-grid"><div class="package"><span class="micro">PRODUCT / 产品包</span><h3>日常使用</h3><p>既有WPF窗口 + 登录Feature<br>不携带VM工具与VM Addin</p><div class="manifest"><code>login.launch.addin.xml</code><label class="switch"><input type="checkbox" id="addin-demo" checked><span>启用声明（示意）</span></label></div><div id="addin-result" class="addin-result" aria-live="polite">声明有效 → 装配登录Feature</div></div>
          <div class="shared-core"><span>ONE CORE</span><h3>同源后端</h3><p>Domain<br>Application<br>Platform<br>Bootstrap</p><small>双包共用DLL<br>SHA256一致性检查</small></div>
          <div class="package"><span class="micro">DEVELOPMENT / 独立工具包</span><h3>开发验证</h3><p>共用Model CLI + 实验脚手架<br>配置与授权独立，不复制启动链</p><div class="gate-pills"><span>VM Addin</span><b>+</b><span>本机显式授权</span><b>+</b><span>目标检查</span></div><p class="micro">以上同时满足，才允许明确的VM操作。<br>本页不会启动虚拟机。</p></div></div><div class="scope-note">当前：已编译Feature的声明式装配。未实现：CAF API适配、任意DLL动态加载与热卸载。示意开关不修改真实清单。</div>'''
    if kind == "maintenance":
        return '''<div class="library"><div class="canonical"><span class="micro">ONE CANONICAL FEATURE</span><strong>八股事实</strong><p>价值 / 范围 / 操作 / 约束<br>前端 / 后端 / 扩展 / 验证</p>''' + link('阅读唯一功能正文','../features/launch-by-profile.html') + '''</div><div class="reading-paths"><a href="../manuals/user.html" target="_blank" rel="noopener"><span>01 / USER</span><h3>使用手册</h3><p>先准备，再操作，知道如何核验。</p><b>↗</b></a><a href="../manuals/developer.html" target="_blank" rel="noopener"><span>02 / DEVELOPER</span><h3>开发手册</h3><p>从功能，直达脚本与系统边界。</p><b>↗</b></a><a href="../manuals/demo.html" target="_blank" rel="noopener"><span>03 / DEMO</span><h3>核心演示</h3><p>按观众深度编排，同源引用事实。</p><b>↗</b></a></div></div><div class="trace-bar"><span>需求ID / OpenSpec</span><b>→</b><span>行为场景</span><b>→</b><span>具体源码</span><b>→</b><span>分批证据</span></div>'''
    if kind == "evidence":
        return '<div class="metrics">'+''.join(f'<div><strong>{e(n)}</strong><h3>{e(t)}</h3><small>{e(b)}</small></div>' for n,t,b in s['metrics'])+'''</div><p class="metric-note">证据日期 2026-10-28 · 批次存在重叠，不相加为总覆盖 · 本作品集未重跑产品测试</p><div class="evidence-bottom"><div><span class="micro">SIGNAL ≠ OUTCOME</span><h3>Created ≠ 登录成功</h3><p>List&lt;Record&gt;与NLog按AttemptId关联。进程创建、窗口、许可和工程登录分别核实；创建后不确定则保留事实。</p></div><div class="boundary"><span class="micro">ARCHITECTURE GUARDRAILS</span><h3>边界可被检查</h3><p>ArchUnitNET约束依赖方向，保留真实违规夹具；UI美观与厂商协议不由架构测试代替。</p></div></div>'''
    return '''<div class="delivery-board"><div><span class="status-tag done">已实现</span><h3>可运行的核心链路</h3><p>发现 / 同一Model编辑<br>快照 / 后端执行 / 记录<br>XML Addin / 双包同源</p></div><div><span class="status-tag planned">拟议 / 待实施</span><h3>下一段产品能力</h3><p>远程连接配置向导<br>TOML迁移 / 共享凭据授权<br>FigCraft新界面 / CAF适配</p></div><div><span class="status-tag history">历史证据</span><h3>保留事实与边界</h3><p>E3D原生启动与许可签出<br>隔离VM网络与SMB验收<br>不代表当前环境已验收</p></div></div><div class="closing"><p>一个Feature，既能向客户讲清，<br>也能让维护者找到下一行代码。</p><div>''' + link('打开交付状态','../status.html','primary') + link('下载可编辑 FigCraft','design/launchdesk-portfolio.figcraft') + link('阅读设计与事实来源','README.html') + '</div></div>'


slides=[]
for i,s in enumerate(DATA['slides']):
    refs=''.join(link(a,b) for a,b in s['refs'])
    slides.append(f'''<section class="slide {s['kind']} {'dark' if s['kind'] in ['cover','execution'] else ''}" id="{s['id']}" data-title="{e(s['label'])}" aria-label="第{i+1}幕 {e(s['label'])}" >
      <div class="slide-top"><span>LAUNCHDESK.NEXT</span><span>ENGINEERING CASE STUDY / {e(s['eyebrow'])}</span></div>
      <div class="slide-content"><header class="slide-heading"><span class="eyebrow">{e(s['eyebrow'])}</span><h{'1' if i==0 else '2'}>{e(s['title']).replace(chr(10),'<br>')}</h{'1' if i==0 else '2'}><p class="lead">{e(s['lead'])}</p></header>{visual(s)}</div>
      <div class="slide-bottom"><span>{e(s['takeaway'])}</span><div class="refs">{refs}</div></div></section>''')
toc=''.join(f'<button data-slide="{i}"><span>0{i+1}</span>{e(s["label"])}<small>{e(s["eyebrow"].split(" / ")[-1])}</small></button>' for i,s in enumerate(DATA['slides']))
page='''<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><meta name="description" content="LaunchDesk.Next 项目作品集：以项目经理视角呈现易用性、模型边界、执行链路、可维护性和交付证据。"><title>'''+e(DATA['title'])+'''</title><link rel="stylesheet" href="tokens.css"><link rel="stylesheet" href="style.css"><link rel="stylesheet" href="figcraft.css"></head><body>
<a href="#deck" class="skip">跳到演示正文</a><main id="deck" tabindex="-1">'''+''.join(slides)+'''</main>
<nav class="deck-controls" aria-label="演示控制"><button id="toc-open" aria-label="打开八幕目录">☷ <span>目录</span></button><div class="progress"><span id="progress-fill"></span></div><span class="page-count" id="page-count" aria-live="polite">01 / 08</span><span id="page-title">项目价值</span><button id="previous" aria-label="上一幕">←</button><button id="next" aria-label="下一幕">→</button><button id="fullscreen" aria-label="切换全屏"><span>全屏</span> ⛶</button><button id="print-deck" aria-label="打印全部八幕">打印 ↗</button></nav>
<dialog id="toc"><div class="toc-top"><span>THE STORY / 八幕目录</span><button id="toc-close" aria-label="关闭目录">关闭 ×</button></div><h2>从价值，到下一步。</h2><div class="toc-grid">'''+toc+'''</div><p>← → 翻页 · Home / End 首尾 · Esc 关闭目录 · 无自动播放</p></dialog><noscript><p class="noscript">当前浏览器未启用JavaScript：以下完整八幕仍可顺序阅读。</p></noscript>
<script type="application/json" id="portfolio-data">'''+json.dumps(DATA,ensure_ascii=False).replace('</',r'<\/')+'''</script><script src="app.js"></script></body></html>'''
for asset in ['tokens.css','style.css','figcraft.css']:
    if (OUT/asset).exists():
        digest=hashlib.sha256((OUT/asset).read_bytes()).hexdigest()[:12]
        page=page.replace('href=\"'+asset+'\"','href=\"'+asset+'?v='+digest+'\"')
(OUT/'index.html').write_text(page,encoding='utf-8')
print('HTML generated; FigCraft is imported from the captured browser DOM, not independently drawn.')


