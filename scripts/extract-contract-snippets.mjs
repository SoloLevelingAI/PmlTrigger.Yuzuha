import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const output = path.join(root, 'docs/presentation/contract-atlas/snippets.js');
const windowLines = 32;
const githubRepo = 'https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha';

/** Reviewed symbol bindings. Line numbers are derived, never authored. */
const atlasBindings = [
  { id: 'script:workbench-entry', entityIds: ['script:workbench-entry'], path: 'src/YuzuhaToolkit.Desktop/PreparationWorkbench.cs', symbol: 'PreparationWorkbench', kind: 'type', language: 'csharp' },
  { id: 'script:launch-workflow', entityIds: ['script:launch-workflow'], path: 'src/YuzuhaToolkit.Manager.Core/LaunchWorkflow.cs', symbol: 'LaunchWorkflowService', kind: 'type', language: 'csharp' },
  { id: 'script:communication-client', entityIds: ['script:communication-client'], path: 'src/YuzuhaToolkit.Manager.Core/CommunicationCliClient.cs', symbol: 'CommunicationCliClient', kind: 'type', language: 'csharp' },
  { id: 'script:frozen-copy', entityIds: ['script:frozen-copy'], path: 'src/YuzuhaToolkit.Manager.Core/RuntimeDeploymentService.cs', symbol: 'RuntimeDeploymentService', kind: 'type', language: 'csharp' },
  { id: 'script:pml-development', entityIds: ['script:pml-development'], path: 'scripts/Invoke-PmlDevelopment.ps1', symbol: '[CmdletBinding()]', kind: 'literal', language: 'powershell' },
  { id: 'script:array-window', entityIds: ['script:array-window'], path: 'src/Shared/PmlArrayWindow.cs', symbol: 'PmlArrayWindow', kind: 'type', language: 'csharp' },
  { id: 'script:dbref-reader', entityIds: ['script:dbref-reader'], path: 'PMLLIB/Traversal/YuzuhaReadDbref.pmlfnc', symbol: 'YuzuhaReadDbref', kind: 'word', language: 'pml' },
  { id: 'script:host-bootstrap', entityIds: ['script:host-bootstrap'], path: 'installer/Yuzuha.iss', symbol: 'CurStepChanged', kind: 'member', language: 'pascal' },
  { id: 'script:ribbon-frozen-deployment', entityIds: ['script:ribbon-frozen-deployment'], path: 'src/YuzuhaToolkit.Manager.Core/RibbonHttpAdapter.cs', symbol: 'RibbonHttpAdapter', kind: 'type', language: 'csharp' },
  { id: 'script:migration-preview', entityIds: ['script:migration-preview'], path: 'src/YuzuhaToolkit.Manager.Core/MigrationPreview.cs', symbol: 'MigrationPreview', kind: 'type', language: 'csharp' },
  { id: 'script:knowledge-import', entityIds: ['script:knowledge-import'], path: 'src/YuzuhaToolkit.Knowledge.Core/KnowledgeRepository.cs', symbol: 'KnowledgeRepository', kind: 'type', language: 'csharp' },
  { id: 'script:sdk-acceptance', entityIds: ['script:sdk-acceptance'], path: 'scripts/run-documented-test.ps1', symbol: 'DocumentedTestResults.ps1', kind: 'literal', language: 'powershell' }
];

function owned(relative) {
  if (typeof relative !== 'string' || relative.includes('..') || path.isAbsolute(relative)) throw new Error('Snippet path escapes repository: ' + relative);
  const absolute = path.resolve(root, relative);
  if (!absolute.startsWith(root + path.sep) || !fs.existsSync(absolute) || !fs.statSync(absolute).isFile()) throw new Error('Snippet file missing: ' + relative);
  return absolute;
}

export function locateSymbol(text, symbol, kind) {
  const lines = text.split(/\r?\n/);
  const escaped = symbol.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const pattern = kind === 'type' ? new RegExp('\\b(?:class|record|struct|interface)\\s+' + escaped + '\\b')
    : kind === 'member' ? new RegExp('\\b' + escaped + '\\s*\\(')
    : kind === 'word' ? new RegExp('\\b' + escaped + '\\b')
    : null;
  const hits = [];
  lines.forEach((line, index) => {
    if (kind === 'literal' ? line.includes(symbol) : pattern.test(line)) hits.push(index + 1);
  });
  if (!hits.length) throw new Error('Symbol not found: ' + symbol + ' (' + kind + ')');
  return hits[0];
}

export function sliceFragment(text, startLine) {
  const lines = text.split(/\r?\n/);
  const start = startLine;
  const end = Math.min(lines.length, start + windowLines - 1);
  return { startLine: start, endLine: end, text: lines.slice(start - 1, end).join('\n') };
}

function languageOf(relative) {
  if (relative.endsWith('.iss')) return 'pascal';
  if (/\.pml/.test(relative)) return 'pml';
  if (relative.endsWith('.ps1')) return 'powershell';
  if (relative.endsWith('.py')) return 'python';
  return 'csharp';
}

export function collectBindings() {
  const journey = JSON.parse(fs.readFileSync(path.join(root, 'docs/contracts/manager-journey.json'), 'utf8'));
  const operations = [];
  for (const stage of journey.journey.stages) {
    for (const script of stage.scripts) {
      operations.push({
        id: 'member:' + script.typeName + '.' + script.member,
        entityIds: ['feature:launch', 'feature:deployment'],
        path: script.path,
        symbol: script.member,
        typeName: script.typeName,
        kind: 'member',
        language: languageOf(script.path)
      });
    }
  }
  const seen = new Set();
  return [...atlasBindings, ...operations].filter(item => {
    if (seen.has(item.id)) throw new Error('Duplicate snippet id: ' + item.id);
    seen.add(item.id);
    return true;
  });
}

function namespaceBefore(lines, line) {
  let ns = '';
  for (let index = 0; index < line; index++) {
    const match = lines[index].match(/^namespace\s+([A-Za-z0-9_.]+)\s*;?/);
    if (match) ns = match[1];
  }
  return ns;
}
function enclosingType(lines, line) {
  for (let index = line - 1; index >= 0; index--) {
    const match = lines[index].match(/\b(?:class|record|struct|interface)\s+([A-Za-z_][A-Za-z0-9_]*)/);
    if (match) return match[1];
  }
  return '';
}
export function apiTarget(binding, source, startLine) {
  if (binding.language !== 'csharp') return null;
  const lines = source.split(/\r?\n/);
  const ns = namespaceBefore(lines, startLine);
  const type = binding.kind === 'type' ? binding.symbol : (binding.typeName ? binding.typeName.split('.').at(-1) : enclosingType(lines, startLine));
  const typeUid = binding.typeName || (ns ? ns + '.' + type : type);
  if (!typeUid) return null;
  const project = binding.path.startsWith('src/YuzuhaToolkit.Manager.Core/') ? 'manager'
    : binding.path.includes('PmlArrayWindow.cs') || binding.path.startsWith('src/YuzuhaToolkit.Mcp.Core/') ? 'mcp'
    : binding.path.startsWith('src/YuzuhaToolkit.Knowledge.Core/') ? 'knowledge'
    : binding.path.startsWith('src/YuzuhaToolkit.Desktop/') ? 'desktop'
    : binding.path.startsWith('installer/SetupBridge/') ? 'setup-bridge'
    : binding.path.startsWith('src/YuzuhaToolkit.Workspace/') ? 'workspace'
    : binding.path.startsWith('src/YuzuhaToolkit.WebApps/') ? 'webapps'
    : binding.path.startsWith('src/YuzuhaToolkit.DevHost/') ? 'devhost'
    : null;
  if (!project) throw new Error('No DocFX project for ' + binding.path);
  return {
    project,
    typeUid,
    member: binding.kind === 'member' ? binding.symbol : null,
    href: '../../../artifacts/docfx/site/api/' + project + '/' + typeUid + '.html'
  };
}
export function buildSnippets() {
  const items = collectBindings().map(binding => {
    const absolute = owned(binding.path);
    const source = fs.readFileSync(absolute, 'utf8');
    const startLine = locateSymbol(source, binding.symbol, binding.kind);
    const fragment = sliceFragment(source, startLine);
    if (!fragment.text.includes(binding.kind === 'literal' ? binding.symbol : binding.symbol)) throw new Error('Fragment lost its symbol: ' + binding.id);
    const api = apiTarget(binding, source, startLine);
    const githubHref = githubRepo + '/blob/main/' + binding.path + '#L' + fragment.startLine + '-L' + fragment.endLine;
    return { ...binding, ...fragment, api, githubHref };
  });
  return { schema: 'contract-atlas-snippets/1', windowLines, items };
}

function emit(catalog) {
  return '/* Generated by scripts/extract-contract-snippets.mjs. Locate symbols again; do not hand-edit line numbers. */\nglobalThis.CONTRACT_ATLAS_SNIPPETS = ' + JSON.stringify(catalog, null, 2) + ';\n';
}

const isDirect = process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isDirect) {
  const catalog = buildSnippets();
  const text = emit(catalog);
  if (process.argv.includes('--check')) {
    const current = fs.existsSync(output) ? fs.readFileSync(output, 'utf8') : '';
    if (current !== text) throw new Error('Contract snippets drifted from symbol locations; run node scripts/extract-contract-snippets.mjs');
    console.log(JSON.stringify({ mode: 'check', items: catalog.items.length }));
  } else {
    fs.writeFileSync(output, text);
    console.log(JSON.stringify({ mode: 'build', items: catalog.items.length, output: path.relative(root, output) }));
  }
}
