import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';

// Presentation only. All method identities and edges come from Roslyn, never name matching.
export function publishUiTrace({root, output, types}) {
  const graph = JSON.parse(fs.readFileSync(path.join(root, 'artifacts/docfx/ui-trace.json'), 'utf8'));
  const metadata = JSON.parse(fs.readFileSync(path.join(root, 'artifacts/docfx/ui-trace-metadata.json'), 'utf8'));
  assert.equal(graph.kind, 'static-source-trace');
  const nodes = new Map(graph.nodes.map(n => [n.id, n]));
  assert.equal(nodes.size, graph.nodes.length);
  for (const e of graph.edges) assert.ok(nodes.has(e.from) && nodes.has(e.to), 'Unresolved graph identity');
  const roots = graph.entries.filter(e => e.view === 'YuzuhaToolkit.Desktop.SessionWorkbenchView' && e.member === 'Click' && e.expression === 'ReadObject');
  assert.equal(roots.length, 1, 'Real object-query entry must resolve uniquely');
  assert.equal(roots[0].resolution, 'resolved');
  const outgoing = new Map();
  for (const e of graph.edges) { if (!outgoing.has(e.from)) outgoing.set(e.from, []); outgoing.get(e.from).push(e); }
  const queue = [{id: roots[0].target, steps: []}], seen = new Set();
  let chain;
  while (queue.length) {
    const current = queue.shift(); if (seen.has(current.id)) continue; seen.add(current.id);
    if (current.id.startsWith('PlantHost.Rpc.Net10::M:PlantHost.Rpc.RpcClient.InvokeAsync``2(PlantHost.Rpc.RpcMethod{')) { chain = current.steps; break; }
    for (const edge of outgoing.get(current.id) ?? []) queue.push({id: edge.to, steps: [...current.steps, edge]});
  }
  assert.ok(chain?.length, 'No real UI → RPC source relationship path');
  assert.ok(chain.some(e => e.kind === 'possible-implementation'), 'Interface uncertainty must remain visible');
  const selected = new Set([roots[0].target, ...chain.map(e => e.to)]);
  // Keep the verification wrapper next to the lambda-body relationship. The diagram is not an execution order.
  for (const e of graph.edges) if (selected.has(e.from) && e.to.includes('.YuzuhaRpcBridge.RunVerifiedAsync(')) selected.add(e.to);
  const hash = value => createHash('sha256').update(value).digest('hex').slice(0, 16);
  const md = value => String(value ?? '').replace(/[\\[\]<>|]/g, c => '\\' + c).replaceAll('\n', ' ');
  const html = value => String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
  const write = (file, text) => { const dest = path.join(output, 'ui-trace', file); fs.mkdirSync(path.dirname(dest), {recursive: true}); fs.writeFileSync(dest, text); };
  const page = id => 'method-' + hash(id) + '.md';
  const copied = new Set();
  const source = (file, line) => {
    if (!file) return '本次编译集中未分析源码的声明';
    const key = hash(file), absolute = path.resolve(root, file);
    assert.ok(absolute.startsWith(path.resolve(root) + path.sep), 'Source outside repository');
    if (!copied.has(key)) {
      const bytes = fs.readFileSync(absolute);
      const expected = graph.nodes.find(n => n.file === file)?.sourceSha256;
      if (expected) assert.equal(createHash('sha256').update(bytes).digest('hex'), expected, 'Source changed after compiler snapshot: ' + file);
      const lines = bytes.toString('utf8').split(/\r?\n/);
      write('sources/' + key + '.md', '# ' + md(path.basename(file)) + '\n\n本次编译源码快照：`' + file + '`。不是执行记录。\n\n<pre><code class="nohighlight">' + lines.map((text, i) => '<span id="L' + (i + 1) + '">' + (i + 1) + '  ' + html(text) + '</span>').join('\n') + '</code></pre>\n');
      copied.add(key);
    }
    return '[' + md(path.basename(file)) + ':' + line + '](sources/' + key + '.md#L' + line + ')';
  };
  const label = id => nodes.get(id).name.split('(')[0].split('.').slice(-2).join('.');
  const link = id => selected.has(id) ? '[' + md(label(id)) + '](' + page(id) + ')' : '`' + md(label(id)) + '`';
  const relationship = kind => ({call: '源码调用', 'interface-call': '接口声明调用', 'possible-implementation': '可能的接口实现', 'possible-event-handler': '可能的事件处理器', 'event-raise': '事件触发源码', 'virtual-dispatch': '虚方法声明，运行时实现未定', 'delegate-invoke': '委托调用，目标未追踪', 'property-write': '写入属性 setter'}[kind] ?? (kind.startsWith('callback-body:') ? '回调体中的' + relationship(kind.slice(14)) : kind));
  const notice = '**静态源码关系，非调用栈、执行顺序或通过证据。** 回调体存在调用不代表该回调已执行；接口实现只列候选。\n\n';
  for (const id of selected) {
    const node = nodes.get(id), incoming = graph.edges.filter(e => e.to === id), calls = outgoing.get(id) ?? [];
    const owner = types.filter(t => id.startsWith(t.project + '::M:' + t.originalUid + '.')).sort((a,b) => b.originalUid.length - a.originalUid.length)[0];
    let text = '# ' + md(node.name) + '\n\n' + notice + '**符号 ID：** `' + node.id + '`\n\n**声明：** ' + source(node.file, node.line) + (node.generated ? ' · 编译生成源码' : '') + '\n\n';
    if (owner) {
      text += '[所属类型、API 与测试关联](~/artifacts/docfx/navigation/' + owner.sourceUrl.replace(/^reference\//, '').replace(/\.html$/, '.md') + ')\n\n';
      fs.appendFileSync(path.join(output, owner.sourceUrl.replace(/^reference\//, '').replace(/\.html$/, '.md')), '\n## UI 溯源 · ' + hash(id) + '\n\n[查看 ' + md(node.name) + ' 的入口、调用方与下游](~/artifacts/docfx/navigation/ui-trace/' + page(id) + ')\n');
    }
    const uiReferences = graph.entries.filter(e => e.target === id).map(e => '| [' + md(e.view.split('.').at(-1) + ' / ' + e.control) + '](index.md) | XAML ' + md(e.member) + ' | ' + source(e.file, e.line) + ' |');
    text += '## 谁引用此方法\n\n| 来源 | 关系 | 源码位置 |\n| --- | --- | --- |\n' + [...uiReferences, ...incoming.map(e => '| ' + link(e.from) + ' | ' + relationship(e.kind) + ' | ' + source(e.file, e.line) + ' |')].join('\n') + '\n\n';
    text += '## 此方法涉及哪些调用\n\n| 目标 | 关系 | 源码位置 |\n| --- | --- | --- |\n' + calls.map(e => '| ' + link(e.to) + ' | ' + relationship(e.kind) + ' | ' + source(e.file, e.line) + ' |').join('\n') + '\n\n';
    text += graph.boundaries.filter(b => b.from === id).map(b => '> ' + md(b.reason) + ' `' + md(b.expression) + '`').join('\n\n') + '\n\n[返回 UI 入口与样例链](index.md)\n';
    write(page(id), text);
  }
  let text = '# UI 入口与调用关系\n\n' + notice + '**源码基准：** `' + metadata.commit + '`；工作区修改：`' + metadata.dirty + '`。环境：' + md(metadata.environment) + '。\n\n[读取范围与限制](~/docs/reference/ui-tracing.md) · [完整符号/关系 JSON](graph.json) · [构建身份](metadata.json)\n\n';
  text += '## 真实样例：读取所选对象\n\nXAML 入口：' + source(roots[0].file, roots[0].line) + '。\n\n| 起点 | 关系 | 下一步 | 位置 |\n| --- | --- | --- | --- |\n' + chain.map(e => '| ' + link(e.from) + ' | ' + relationship(e.kind) + ' | ' + link(e.to) + ' | ' + source(e.file, e.line) + ' |').join('\n') + '\n\n';
  text += 'RunVerifiedAsync 是同一桥方法直接调用的核验包装器，可从桥方法详情追溯。表中回调体指向服务的边不表示绕过包装器；当前分析没有跨方法委托数据流或控制流证明。RPC 发送以后需结合 Host 路由、PML 源码和同次 RequestId 日志判断实际执行。\n\n';
  text += '## 本次 XAML 入口/绑定清单\n\n当前 WPF 主要采用事件处理代码；没有把它虚构成 ViewModel。未能确定的绑定保留原因。\n\n| View / 控件 | 事件或绑定 | 目标 / 原因 | 源码 |\n| --- | --- | --- | --- |\n' + graph.entries.map(e => '| ' + md(e.view.split('.').at(-1) + ' / ' + e.control) + ' | ' + md(e.member + ': ' + e.expression) + ' | ' + (e.target ? link(e.target) : md(e.reason)) + ' | ' + source(e.file, e.line) + ' |').join('\n') + '\n';
  write('index.md', text); write('graph.json', JSON.stringify(graph, null, 2)); write('metadata.json', JSON.stringify(metadata, null, 2));
  write('sample.json', JSON.stringify({entry: roots[0], chain}, null, 2));
  return {entries: graph.entries.length, unresolved: graph.entries.filter(e => !e.target).length, sampleEdges: chain.length};
}
