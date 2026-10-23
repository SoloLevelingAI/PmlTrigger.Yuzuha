import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

export function filterItems(items,query) {
  const search=String(query).toLocaleLowerCase();
  return items.filter(item=>[item.Path,item.Type,item.Value].some(value=>String(value??'').toLocaleLowerCase().includes(search)));
}

export function csvRows(items) {
  const quote=value=>{let text=String(value??'');if(/^[=+@-]/.test(text))text="'"+text;return '"'+text.replaceAll('"','""')+'"'};
  return [['Path','Type','Value'],...items.map(item=>[item.Path,item.Type,item.Value])].map(row=>row.map(quote).join(',')).join('\r\n');
}

export function readTableSnapshot(value) {
  if(value?.steps) {
    const step=value.steps.find(s=>s.name==='read-results');
    if(!step?.result)throw new Error('The workflow has no returned data.');
    return {title:'AVEVA 开发验证结果',status:value.status,steps:value.steps.map(s=>({name:s.name,status:s.status})),expected:value.manifest?.expected??{},items:step.result.Items,unparsed:step.result.UnparsedCount??0};
  }
  const result=value?.fullFormattedRows?.data??value?.data??value;
  if(!Array.isArray(result?.Items))throw new Error('Select a JSON result containing Items.');
  return {title:'AVEVA 数据表',status:'Snapshot',steps:[],expected:{},items:result.Items,unparsed:result.UnparsedCount??0};
}

export function renderPmlDataTable(value) {
  const snapshot=readTableSnapshot(value);
  const data=JSON.stringify(snapshot).replaceAll('<','\\u003c').replaceAll('\u2028','\\u2028').replaceAll('\u2029','\\u2029');
  return `<!doctype html>
<html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>AVEVA 数据表</title>
<style>
:root{color-scheme:light;font-family:Inter,"Segoe UI","Microsoft YaHei",sans-serif;color:#203340;background:#edf2f3}*{box-sizing:border-box}body{margin:0}main{max-width:1220px;margin:48px auto;padding:0 28px}.eyebrow{font-size:12px;letter-spacing:.12em;color:#337265;font-weight:700}h1{font-size:32px;margin:12px 0}p{color:#627380;line-height:1.7}.top{display:flex;align-items:center;justify-content:space-between;gap:24px}.pill{background:#dbede7;color:#1c6554;padding:8px 14px;border-radius:20px;font-size:13px;white-space:nowrap}.stats{display:flex;gap:12px;margin:26px 0}.stat{background:white;border:1px solid #dce5e7;border-radius:12px;padding:18px 24px;min-width:160px}.stat b{display:block;font-size:28px;margin-bottom:4px}.stat span{font-size:13px;color:#71818b}.panel{border:1px solid #d8e1e4;background:#fff;border-radius:14px;overflow:hidden;box-shadow:0 12px 35px #29434e08}.toolbar{padding:18px 20px;display:flex;align-items:center;gap:10px;flex-wrap:wrap;border-bottom:1px solid #e6edef}input[type=search]{border:1px solid #cbd8dc;border-radius:7px;padding:10px 12px;font:inherit;width:280px;margin-right:auto}button,.file{font:inherit;font-size:14px;cursor:pointer;border:1px solid #c9d7da;border-radius:7px;background:#fff;padding:10px 16px;color:#2c4651}button.primary{background:#1e6557;border-color:#1e6557;color:white}.file input{display:none}.scroll{max-height:560px;overflow:auto}table{border-collapse:collapse;width:100%;font-size:14px}thead{position:sticky;top:0;background:#f4f7f8;text-align:left;z-index:1}th{font-weight:600;color:#617581;font-size:12px}th,td{padding:14px 20px;border-bottom:1px solid #e8eef0}td.path{font-family:Consolas,monospace;font-size:13px}td.value{white-space:pre-wrap;overflow-wrap:anywhere;max-width:500px}.good{color:#23745c}.bad{color:#ac4538}.foot{padding:14px 20px;font-size:12px;color:#72838c}.steps{margin-top:26px;background:white;border:1px solid #dce5e7;border-radius:12px;padding:18px 22px}summary{cursor:pointer;color:#45616e}.steps ol{line-height:2;color:#627380;padding-left:20px}.empty{padding:36px;text-align:center;color:#748790}@media(max-width:700px){main{margin:24px auto;padding:0 12px}.top{display:block}h1{font-size:25px}.stats{gap:6px}.stat{min-width:0;flex:1;padding:14px}.toolbar input[type=search]{width:100%}th,td{padding:12px}}
</style>
<main><div class="eyebrow">YUZUHA · DATA</div><div class="top"><div><h1 id="title"></h1><p>从 AVEVA 读取的结果快照。筛选、查看和导出均在本地完成。</p></div><span class="pill" id="status"></span></div>
<div class="stats"><div class="stat"><b id="total"></b><span>返回行数</span></div><div class="stat"><b id="visible"></b><span>当前显示</span></div><div class="stat"><b id="unparsed"></b><span>未解析行</span></div></div>
<section class="panel"><div class="toolbar"><input id="search" type="search" aria-label="筛选结果" placeholder="搜索路径、类型或值…"><label class="file">导入读取结果<input id="import" type="file" accept=".json,application/json"></label><button class="primary" id="export">导出当前表格 CSV</button></div><div class="scroll"><table><thead><tr><th>路径</th><th>类型</th><th>实际值</th><th>预期值</th><th>核对</th></tr></thead><tbody id="rows"></tbody></table><div class="empty" id="empty" hidden>没有匹配结果</div></div><div class="foot" id="message">此页面展示已有结果；重新执行读取后，可导入新的 JSON 文件。</div></section><details class="steps" id="steps"><summary>执行步骤</summary><ol id="step-list"></ol></details></main>
<script id="snapshot" type="application/json">${data}</script>
<script>
${filterItems.toString()}
${csvRows.toString()}
let state=JSON.parse(document.getElementById('snapshot').textContent),shown=[];
const byId=id=>document.getElementById(id);
function render(){
 shown=filterItems(state.items,byId('search').value);
 byId('title').textContent=state.title;byId('status').textContent=state.status==='Passed'?'本次验证通过':state.status==='Snapshot'?'读取结果快照':'验证失败或结果未知';
 byId('total').textContent=state.items.length;byId('visible').textContent=shown.length;byId('unparsed').textContent=state.unparsed;
 const fragment=document.createDocumentFragment();
 for(const item of shown){const row=document.createElement('tr'),has=Object.hasOwn(state.expected,item.Path),expected=has?state.expected[item.Path]:'—';
 const values=[item.Path,item.Type,item.Value,expected,has?(String(item.Value)===expected?'匹配':'不匹配'):'—'];
 values.forEach((value,i)=>{const cell=document.createElement('td');cell.textContent=String(value??'');if(i===0)cell.className='path';if(i===2)cell.className='value';if(i===4&&has)cell.className=values[4]==='匹配'?'good':'bad';row.append(cell)});fragment.append(row)}
 byId('rows').replaceChildren(fragment);byId('empty').hidden=shown.length>0;
 byId('steps').hidden=!state.steps.length;byId('step-list').replaceChildren(...state.steps.map(step=>{const item=document.createElement('li');item.textContent=step.name+' · '+step.status;return item}));
}
byId('search').addEventListener('input',render);
byId('export').addEventListener('click',()=>{
 const csv=csvRows(shown);
 const url=URL.createObjectURL(new Blob(['\\ufeff'+csv],{type:'text/csv;charset=utf-8'})),a=document.createElement('a');a.href=url;a.download='aveva-results.csv';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
 byId('message').textContent='已导出当前 '+shown.length+' 行。';
});
byId('import').addEventListener('change',async event=>{try{
 const value=JSON.parse(await event.target.files[0].text());
 if(value.steps){const result=value.steps.find(s=>s.name==='read-results')?.result;if(!result)throw Error('未找到读取结果');state={title:'AVEVA 开发验证结果',status:value.status,steps:value.steps,expected:value.manifest?.expected??{},items:result.Items,unparsed:result.UnparsedCount??0}}
 else{const result=value.fullFormattedRows?.data??value.data??value;if(!Array.isArray(result.Items))throw Error('JSON 中没有 Items 数组');state={title:'AVEVA 数据表',status:'Snapshot',steps:[],expected:{},items:result.Items,unparsed:result.UnparsedCount??0}}
 byId('search').value='';render();byId('message').textContent='已导入 '+event.target.files[0].name;
 }catch(error){byId('message').textContent='导入失败：'+error.message}});
render();
</script></html>`;
}

if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  if(process.argv.length!==4)throw new Error('Usage: node scripts/pml-data-table.mjs <result.json> <output.html>');
  const value=JSON.parse(fs.readFileSync(process.argv[2],'utf8').replace(/^\uFEFF/,''));
  fs.writeFileSync(process.argv[3],renderPmlDataTable(value));
  console.log(process.argv[3]);
}
