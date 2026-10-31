// Read-only source inventory. Never runs a source repository's scripts.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
const root = path.resolve(import.meta.dirname, '..');
const output = path.join(root, 'openspec/changes/integrate-pmltwin-product-platform/baseline');
const roots = {
  Archon: path.join(root, 'integrations/rewrite/source'),
  Next: path.join(root, 'integrations/next/source'), Ribbon: path.join(root, 'integrations/ribbon/source'), PMLTWIN: root,
};
const git = (cwd, ...args) => { try { return execFileSync('git', ['-c','core.quotepath=false',...args], {cwd,encoding:'utf8',stdio:['ignore','pipe','pipe'],maxBuffer:32*1024*1024}).trim(); } catch { return null; } };
const hash = data => crypto.createHash('sha256').update(data).digest('hex');
const allowed = new Set('.cs .csproj .sln .slnx .props .targets .json .md .txt .xml .xaml .yml .yaml .ps1 .psm1 .mjs .js .ts .tsx .html .css .resx .config .bat .cmd .pml .pmlfnc .pmlobj .pmlfrm .toml .svg .py .gitignore .editorconfig'.split(' '));
const excluded = /(^|\/)(bin|obj|node_modules|\.git|\.vs|artifacts|publish|release|dist|baseline)(\/|$)|(^|\/)(\.env(?:\..*)?|credentials?[^/]*|secrets?[^/]*|[^/]*\.(?:pfx|p12|key|pem|db|sqlite|sqlite3|dll|exe|zip|7z))$/i;
const inventories = {}; const security = [];
for (const [name, cwd] of Object.entries(roots)) {
  if (!fs.existsSync(cwd)) throw Error(`Missing source ${name}`);
  const listed=git(cwd,'ls-files','--cached','--others','--exclude-standard','-z');
  const status=git(cwd,'status','--porcelain=v1','--','.');
  if(listed===null || status===null) throw Error(`Cannot read complete Git inventory/dirty state for ${name}; do not overwrite baseline with a partial sandbox view.`);
  const files = [...new Set(listed.split('\0').filter(Boolean))].sort();
  const records=[]; const omissions={excluded:0,nonText:0,missing:0};
  for (const file of files) {
    const local=path.join(cwd,file); if (!fs.existsSync(local) || !fs.statSync(local).isFile()) { omissions.missing++; continue; }
    const data=fs.readFileSync(local);
    if (name==='PMLTWIN') {
      const findings=[];
      if (/\.(dll|exe|db|sqlite|pfx|p12|key|pem|zip|7z)$/i.test(file)) findings.push('binary-or-sensitive-file-review');
      if (data.length<8*1024*1024 && !data.includes(0)) {
        const text=data.toString('utf8');
        if (/-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/.test(text)) findings.push('private-key');
        if (/\b(?:ghp_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|AKIA[A-Z0-9]{16}|sk-[A-Za-z0-9_-]{30,})\b/.test(text)) findings.push('token-pattern');
      }
      if (findings.length) security.push({path:file,findings});
    }
    if (excluded.test(file)) { omissions.excluded++; continue; }
    if (!allowed.has(path.extname(file).toLowerCase()) && !allowed.has(path.basename(file))) { omissions.nonText++; continue; }
    if (data.includes(0) || data.length>8*1024*1024) { omissions.nonText++; continue; }
    records.push({path:file,sha256:hash(data),bytes:data.length});
  }
  const head=git(cwd,'rev-parse','--verify','HEAD');
  inventories[name]={identity:name==='PMLTWIN'?'current-workspace':'integrated-source',
    path:path.relative(root,cwd).replaceAll('\\','/') || '.',
    head:name==='PMLTWIN'?head:null,
    provenance:name==='PMLTWIN'?null:path.relative(root,path.join(cwd,'../manifest.json')).replaceAll('\\','/'),
    branch:name==='PMLTWIN'?git(cwd,'branch','--show-current'):null,workspaceStatus:status,
    trackedWorkspaceDiffSummary:head?git(cwd,'diff','HEAD','--numstat','--','.'):null,files:records,omissions};
}
const rows=[];
function owner(text) {
  if (/知识|Knowledge|knowledge/.test(text)) return 'knowledge';
  if (/许可|licen|\bLC-|\bVM-|\bDV-/.test(text)) return 'optional-diagnostics';
  if (/变量|EVARS/.test(text)) return 'environment-variables';
  if (/部署|交付|工具来源|Deployment|PluginDeployment/.test(text)) return 'tools-deployment';
  if (/分享|迁移|导入|导出|凭据|秘密|\bCR-|\bPF-/.test(text)) return 'configuration';
  if (/资源|监测|轮询|健康/.test(text)) return 'health';
  if (/文档|作品集|DocFX|\bDOC-|\bUX-/.test(text)) return 'design-evidence';
  if (/发现|环境|\bPM-|\bRP-|模块/.test(text)) return 'environment';
  if (/启动|配置|Launch|launch|\bMD-/.test(text)) return 'launch';
  if (/PML|通信|Host|会话|执行授权|查询/.test(text)) return 'communication';
  return 'composition';
}
const add=(source,id,text,status,locator,original)=>rows.push({source,id,owner:owner(id+' '+text),mappingStatus:'candidate-review-required',sourceStatus:status,locator,original});
const catalog=JSON.parse(fs.readFileSync(path.join(roots.Archon,'docs/reference/specifications/catalog.json'),'utf8'));
for (const r of catalog.records) add('Archon',r.id,r.title+' '+r.description,r.status,'docs/reference/specifications/catalog.json#'+r.id,r);
const next=fs.readFileSync(path.join(roots.Next,'openspec/requirements.md'),'utf8');
for (const line of next.split(/\r?\n/)) { const m=line.match(/^\|\s*([A-Z]+-\d+)\s*\|\s*(.*?)\s*\|\s*(.*?)\s*\|/); if(m) add('Next',m[1],m[2],m[3],'openspec/requirements.md#'+m[1],{request:m[2],status:m[3]}); }
const contract=JSON.parse(fs.readFileSync(path.join(root,'docs/specification/contract.json'),'utf8'));
for (const family of ['goals','requirements','bindings','tests','screens','models']) for (const r of contract[family]) {
  const id=Array.isArray(r)?r[0]:r.id;
  add('PMLTWIN',id,JSON.stringify(r),family==='requirements'?r[4]:'source-preserved','docs/specification/contract.json#'+family+'/'+id,r);
}
const keys=rows.map(r=>r.source+':'+r.id);
// Reviewed accountable owners: explicit identities prevent keywords in linked tests,
// statuses or descriptions from silently moving a requirement to another Feature.
const assignments = {
 Archon: {
  launch:'BRD-001 PRD-002 PRD-003 SRS-004 SRS-006 FRD-002 SDS-003',
  'tools-deployment':'BRD-002 PRD-005 SRS-009 SRS-010 FRD-004 SDS-005',
  configuration:'BRD-003 PRD-006 SRS-002 SRS-005 SRS-012 SRS-013 FRD-001 FRD-006 FRD-007 SDS-001 SDS-007 SDS-008',
  health:'PRD-004 SRS-008 FRD-003 SDS-004',
  'environment-variables':'SRS-011 FRD-005 SDS-006',
  composition:'BRD-004 BRD-005 PRD-001 PRD-007 SRS-001 SRS-003 SRS-007 SRS-014 SDD-001 SDD-002 SDD-003 SDD-004 SDD-005 SDD-006 SDS-002 SDS-009',
  'design-evidence':'FRD-008',
 },
 Next: {
  environment:'PM-01 PM-02 PM-03 PM-04 PM-05 RP-01 RP-02 RP-03',
  configuration:'PF-01 PF-02 CR-01 CR-02 CR-03 CR-04 CR-05 CR-06 CR-07',
  'optional-diagnostics':'VM-01 VM-02 LC-01 LC-02 LC-03 LC-04 LC-05 LC-06 DV-01 DV-02 DV-03',
  launch:'MD-01', composition:'FM-01 AD-01',
  'tools-deployment':'DG-01', 'design-evidence':'UX-01 UX-02 UX-03 UX-04 DOC-01 DOC-02 DOC-03',
 },
 PMLTWIN: {
  composition:'G1 G2 G3 R01 R12 R13 R22 B12 B20 T15 M3',
  environment:'R02 B08 B09 B22 B29 S24',
  launch:'R03 R23 B01 B02 B10 B21 S01 S02 S25 S26 S27 S28 T01 T02 T10',
  configuration:'R04 R05 R06 R14 R15 B03 B04 B05 B06 B07 B23 B25 S03 S04 S05 S06 S07 S08 S09 S30 T03 T04 T05 T06 M1',
  knowledge:'R07 R20 B17 S10 S11',
  communication:'R08 R09 R11 R16 R17 R18 R21 B11 B13 B14 B15 B18 B19 B24 B30 B31 S12 S13 S14 S15 S16 S18 S19 S20 S22 S23 S29 T07 T08 T09 T11 T13 T14 M2',
  'tools-deployment':'R10 R19 B16 S17 T12',
  'design-evidence':'R24 S21',
 },
};
const explicit = new Map();
for (const [source, groups] of Object.entries(assignments)) for (const [feature, ids] of Object.entries(groups)) for (const id of ids.split(' ')) {
 const key=source+':'+id; if(explicit.has(key)) throw Error('Duplicate owner '+key); explicit.set(key,feature);
}
for (const row of rows) {
 const key=row.source+':'+row.id;
 if(!explicit.has(key)) throw Error('Unmapped original '+key);
 row.owner=explicit.get(key); row.mappingStatus='source-accountability-reviewed; implementation-status-unchanged'; explicit.delete(key);
}
if(explicit.size) throw Error('Unknown IDs in mapping: '+[...explicit.keys()].join(','));
if (keys.length!==new Set(keys).size || rows.some(r=>!r.owner)) throw Error('Duplicate identity or missing owner');
fs.mkdirSync(output,{recursive:true});
const write=(name,value)=>fs.writeFileSync(path.join(output,name),JSON.stringify(value,null,2)+'\n');
write('source-manifest.json',{schema:1,generatedAt:new Date().toISOString(),scope:'nonignored source-text allowlist; excludes binaries, databases, secret-named files and generated/build outputs; hashes reflect dirty workspace',sources:inventories});
write('id-feature-map.json',{schema:1,policy:'one accountable owner per source-qualified original ID; explicit ID groups reviewed against original records; original records/status retained; no implementation approval implied',counts:{Archon:catalog.records.length,Next:rows.filter(r=>r.source==='Next').length,PMLTWIN:rows.filter(r=>r.source==='PMLTWIN').length},rows});
write('push-material-review.json',{schema:1,scope:'all tracked and nonignored untracked PMLTWIN files; path-only high-confidence checks; no secret values emitted',limitations:['not an entropy/password detector','binary licenses and provenance require human/source attribution review','ignored runtime directories are outside push candidate set'],findings:security});
console.log(JSON.stringify({files:Object.fromEntries(Object.entries(inventories).map(([k,v])=>[k,v.files.length])),ids:rows.length,securityFindings:security.length}));
