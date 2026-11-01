import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const input=JSON.parse(fs.readFileSync(path.join(root,'docs/contracts/manager-journey.json'),'utf8'));
const journey=input.journey,operations=new Map(input.operations.map(operation=>[operation.id,operation]));
const fields={input:'输入',precondition:'前置',method:'算法与调用',effect:'作用',validation:'校验',receipt:'回执',recovery:'恢复'};
const md=value=>String(value).replaceAll('|','\\|').replaceAll('\n',' ');
if(journey.schema!=='pmltwin.manager-journey/1'||journey.stages.length!==8)throw Error('Unexpected reviewed journey schema/stage set');
let text=`# ${journey.title}\n\n<!-- Generated from ManagerContractCatalog and the actual operation registry; edit the C# contract, then regenerate. -->\n\n${journey.summary}\n\n## 全面契约\n\n用户从一次应用安装开始，在同一管理器选择环境、核对来源、预览EVARS和部署、明确应用并读取回执，之后启动和核验工程。WPF、CLI和只读API共享下列阶段契约与操作目录。原canonical需求状态保持，文档导航和静态源码不代表本轮实机通过。\n\nInno新交互默认将环境准备交给管理器；安装应用不自动写EVARS或启动AVEVA。旧显式aveva/none计划仍按自身范围执行。管理器copy不覆盖Inno应用根，旧所有权先预览再显式移交；未知格式、漂移、权限或并发冲突明确拒绝。\n\n## 任务地图\n\n`;
for(const stage of journey.stages)text+=`- [${stage.title}](#${stage.id})：${stage.method}\n`;
text+='\n## 安装器如何兑现契约\n\n';
for(const [file,explanation]of [
 ['installer/Yuzuha.iss','Inno入口在应用安装成功后，以原交互用户打开准备工作区；静默模式不自动打开。启动参数只携带待核对的载荷根。'],
 ['installer/NativePages.iss','原生安装页面默认选择manager接手模式；旧显式aveva/none计划保持其声明范围。'],
 ['installer/build.py','要求新Manager与Desktop入口，生成逐文件载荷清单；文档、通信CLI、四Host与管理CLI分别交付。']
]){fs.readFileSync(path.join(root,file));text+=`- [${file}](~/artifacts/docfx/navigation/scripts/sources/${createHash('sha256').update(file).digest('hex').slice(0,16)}.md)：${explanation}\n`;}
for(const [index,stage]of journey.stages.entries()){
 if(!/^[a-z][a-z0-9-]*$/.test(stage.id)||new Set(journey.stages.map(s=>s.id)).size!==journey.stages.length)throw Error('Invalid/duplicate stage identity');
 text+=`\n<a id="${stage.id}"></a>\n\n## ${index+1}. ${stage.title}\n\n| 契约字段 | 约定 |\n|---|---|\n`;
 for(const [key,title]of Object.entries(fields)){if(!stage[key]?.trim())throw Error('Missing '+stage.id+'/'+key);text+=`| ${title} | ${md(stage[key])} |\n`;}
 text+='\n### 操作与脚本解释\n\n';
 for(const id of stage.operationIds){const operation=operations.get(id);if(!operation)throw Error('Unregistered journey operation '+id);text+=`- \`${id}\`：${operation.description}；授权：${operation.requiresWrite?'写入 ':''}${operation.requiresLaunch?'启动 ':''}${operation.requiresExecution?'执行 ':''}${operation.requiresPid?'明确目标身份 ':''}${!operation.requiresWrite&&!operation.requiresLaunch&&!operation.requiresExecution&&!operation.requiresPid?'无业务写入授权':''}。\n`;}
 text+='\n';
 for(const script of stage.scripts){
  if(!script.path.startsWith('src/')||script.path.includes('..')||path.isAbsolute(script.path))throw Error('Script path refused');
  const source=fs.readFileSync(path.join(root,script.path),'utf8');
  if(!new RegExp('\\b'+script.member+'\\s*\\(').test(source))throw Error('Script member missing: '+script.typeName+'.'+script.member);
  const sourceId=createHash('sha256').update(script.path).digest('hex').slice(0,16);
  text+=`- **${script.typeName}.${script.member}**：${script.explanation}\n  [DocFX类型与成员](xref:${script.typeName}) · [完整脚本与行号](~/artifacts/docfx/navigation/scripts/sources/${sourceId}.md) · 源文件：\`${script.path}\`。\n`;
 }
 text+=`\n**验收范围：** ${stage.evidenceBoundary}\n`;
}
text+='\n## CLI、OpenAPI和TypeScript\n\n```powershell\nYuzuha.Manager.Cli.exe op invoke manager.contracts --input-stdin\n# stdin: {}\nYuzuha.Manager.Cli.exe op describe deployment.preview\n```\n\nCLI复用同一注册目录和stdin/文件输入，写入、启动、执行分别显式授权。配置保存、冻结计划、写入与Host核验是不同动作。进程启动ticks经十进制字符串传递，禁止经过JavaScript number舍入；失败或unknown不自动重试。\n\n[契约JSON](../contracts/manager-journey.json) · [只读OpenAPI](../contracts/manager-journey.openapi.json) · [TS客户端](../contracts/manager-journey-client.ts) · [可执行JS客户端](../contracts/manager-journey-client.mjs)。OpenAPI只描述实际只读HTTP目录；业务调用由CLI执行，不据此宣称存在HTTP写入路由。\n\n'+input.outputBoundary+'\n\n## 验收与接手\n\n安装与EVARS/部署移交使用可审查计划、备份和恢复回执；真实安装、旧所有权移交和厂商文件修改按具体预览取得批准。临时目录测试、WPF合成截图、历史E3D2.1/DESIGN报告、当前实机及人工视觉认可分别记录。\n\n[本轮验证与排除项](../validation/install-to-operation-2026-11-01.json) · [本轮实施契约](~/openspec/changes/integrate-pmltwin-product-platform/features/install-to-operation-journey.md) · [准备界面与共享设计](../design/install-to-operation/README.md) · [开发与维护](documentation.md)。\n';
const output=path.join(root,'docs/reference/install-to-operation.zh-CN.md');
if(process.argv.includes('--check')){if(fs.readFileSync(output,'utf8')!==text)throw Error('Journey documentation drift; regenerate from current Manager catalog');}
else fs.writeFileSync(output,text);
console.log(JSON.stringify({stages:journey.stages.length,operations:new Set(journey.stages.flatMap(s=>s.operationIds)).size,mode:process.argv.includes('--check')?'check':'build'}));
