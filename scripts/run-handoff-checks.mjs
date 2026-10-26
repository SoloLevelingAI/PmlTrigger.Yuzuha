import fs from 'node:fs';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {createHash,randomUUID} from 'node:crypto';
import {fileURLToPath} from 'node:url';

const root=fileURLToPath(new URL('../',import.meta.url));
const startedUtc=new Date().toISOString();
const directory=path.join(root,'artifacts/handoff-validation',startedUtc.replace(/[:.]/g,'-')+'-'+randomUUID().slice(0,8));
fs.mkdirSync(directory,{recursive:true});
const result=spawnSync(process.execPath,['--test','--test-reporter=tap','tests/handoff-specifications.test.mjs'],{cwd:root,encoding:'utf8',maxBuffer:8*1024*1024,env:process.env});
const output=(result.stdout??'')+(result.stderr??'');
fs.writeFileSync(path.join(directory,'tests.tap'),output);process.stdout.write(output);
const files=['docs/specification/contract.json','docs/design/Phayton-Workbench-PRD.figcraft','tests/handoff-specifications.test.mjs','scripts/build-handoff.mjs','artifacts/docfx/navigation/specifications/data/metadata.compact.json'];
const report={schema:1,status:result.status===0?'Passed':'Failed',startedUtc,finishedUtc:new Date().toISOString(),exitCode:result.status,
  passed:Number(output.match(/^# pass (\d+)$/m)?.[1]??0),failed:Number(output.match(/^# fail (\d+)$/m)?.[1]??0),
  scope:'Specification metadata, source evidence and native prototype contracts; no AVEVA or human usability acceptance',
  inputs:Object.fromEntries(files.filter(file=>fs.existsSync(path.join(root,file))).map(file=>[file,createHash('sha256').update(fs.readFileSync(path.join(root,file))).digest('hex')]))};
fs.writeFileSync(path.join(directory,'result.json'),JSON.stringify(report,null,2)+'\n');
fs.writeFileSync(path.join(root,'artifacts/handoff-validation/latest.json'),JSON.stringify({result:path.relative(root,path.join(directory,'result.json')).replaceAll('\\','/'),status:report.status})+'\n');
console.log('Saved:',path.relative(root,path.join(directory,'result.json')));
process.exitCode=result.status??1;
