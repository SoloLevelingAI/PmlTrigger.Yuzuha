import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
async function run(exe,args,cwd,log){
 const chunks=[];let bytes=0;
 await new Promise((resolve,reject)=>{
  const child=spawn(exe,args,{cwd,windowsHide:true,env:process.env});let done=false;
  const finish=(failure)=>{if(done)return;done=true;clearTimeout(timer);failure?reject(failure):resolve();};
  const timer=setTimeout(()=>{child.kill();finish(new Error('DocFX capture timeout (180 seconds)'));},180000);
  const data=b=>{bytes+=b.length;if(bytes>16*1024*1024){child.kill();finish(new Error('DocFX output budget'));}else chunks.push(b);};
  child.stdout.on('data',data);child.stderr.on('data',data);child.on('error',finish);
  child.on('close',code=>finish(code===0?null:new Error('DocFX command failed: '+code)));
 }).finally(()=>log?fs.writeFile(log,Buffer.concat(chunks)):undefined);
 return Buffer.concat(chunks).toString('utf8');
}
async function sourceInputs(repo,dotnet){
 const pending=['src/YuzuhaToolkit.Manager.Core/YuzuhaToolkit.Manager.Core.csproj','src/YuzuhaToolkit.Workspace/YuzuhaToolkit.Workspace.csproj'],seen=new Set(),files=new Set();
 while(pending.length){const project=pending.shift();if(seen.has(project))continue;seen.add(project);files.add(project);
  const raw=await run(dotnet,['msbuild',project,'-nologo','-verbosity:quiet','-getItem:Compile,ProjectReference','-getProperty:MSBuildAllProjects'],repo);
  const result=JSON.parse(raw.slice(raw.indexOf('{')));
  for(const item of [...(result.Items.Compile||[]),...(result.Items.ProjectReference||[])]){
   const absolute=await fs.realpath(item.FullPath),rel=path.relative(repo,absolute).replaceAll('\\','/');
   if(rel.startsWith('../')||path.isAbsolute(rel))throw new Error('DocFX compiler input escapes repository');
   if(rel.endsWith('.csproj'))pending.push(rel);else if(!/(^|\/)(obj|bin)\//.test(rel))files.add(rel);
  }
  for(const p of (result.Properties.MSBuildAllProjects||'').split(';').filter(Boolean)){const rel=path.relative(repo,p).replaceAll('\\','/');if(!rel.startsWith('../')&&!path.isAbsolute(rel)&&!/(^|\/)(obj|bin)\//.test(rel))files.add(rel);}
 }
 for(const p of ['global.json','Directory.Build.props','Directory.Build.targets','Directory.Packages.props','NuGet.config','src/NuGet.config','.config/dotnet-tools.json'])if(await fs.stat(path.join(repo,p)).then(s=>s.isFile(),()=>false))files.add(p);
 if(files.size>512)throw new Error('DocFX compiler input budget (512 files)');
 const inputs={};for(const p of [...files].sort()){const bytes=await fs.readFile(path.join(repo,p));if(bytes.length>8*1024*1024)throw new Error('DocFX compiler input exceeds 8MiB');inputs[p]=sha(bytes);}return inputs;
}
export async function captureDocfx({repository=root,dotnet=process.env.DOTNET_HOST_PATH||'dotnet'}={}){
 const repo=await fs.realpath(repository),base=path.join(repo,'artifacts/contract-atlas-docfx');
 const toolVersion=(await run(dotnet,['docfx','--version'],repo)).trim();if(!/^2\.81\.0(?:\+|$)/.test(toolVersion))throw new Error('Expected pinned DocFX 2.81.0, received '+toolVersion);
 await fs.mkdir(path.join(base,'captures'),{recursive:true});
 const capture=path.join(base,'captures',crypto.randomUUID());await fs.mkdir(capture);
 const config={metadata:[{src:[{files:['src/YuzuhaToolkit.Manager.Core/YuzuhaToolkit.Manager.Core.csproj','src/YuzuhaToolkit.Workspace/YuzuhaToolkit.Workspace.csproj'],src:repo}],dest:path.join(capture,'api')}],build:{content:[{files:['api/**.yml','api/**/*.yml']}],dest:'site',template:['default'],globalMetadata:{_appTitle:'Contract method API snapshot'}}};
 const before=await sourceInputs(repo,dotnet);
 const configFile=path.join(capture,'docfx.json');await fs.writeFile(configFile,JSON.stringify(config,null,2)+'\n');
 await run(dotnet,['docfx','metadata',configFile],repo,path.join(capture,'metadata.log'));
 await run(dotnet,['docfx','build',configFile],repo,path.join(capture,'build.log'));
 const site=path.join(capture,'site'),files=[];
 async function walk(dir){for(const e of await fs.readdir(dir,{withFileTypes:true})){const full=path.join(dir,e.name);if(e.isSymbolicLink())throw new Error('DocFX symlink output rejected');if(e.isDirectory())await walk(full);else if(e.isFile()){const b=await fs.readFile(full);if(b.length>8*1024*1024)throw new Error('DocFX asset exceeds 8MiB');files.push({path:path.relative(site,full).replaceAll('\\','/'),sha256:sha(b),bytes:b.length});}}}
 await walk(site);files.sort((a,b)=>a.path.localeCompare(b.path));
 if(files.length>1500||files.reduce((n,x)=>n+x.bytes,0)>64*1024*1024||!files.some(x=>x.path==='xrefmap.yml'))throw new Error('DocFX site budget or xrefmap missing');
 const after=await sourceInputs(repo,dotnet);if(JSON.stringify(before)!==JSON.stringify(after))throw new Error('DocFX compiler inputs changed during capture; previous snapshot preserved');
 const manifest={schemaVersion:1,tool:'docfx',version:toolVersion,scope:['YuzuhaToolkit.Manager.Core','YuzuhaToolkit.Workspace'],sourceInputs:after,siteRoot:path.relative(repo,site).replaceAll('\\','/'),xrefmap:'xrefmap.yml',files};
 const temporary=path.join(base,'snapshot.'+crypto.randomUUID()+'.tmp');await fs.writeFile(temporary,JSON.stringify(manifest,null,2)+'\n');await fs.rename(temporary,path.join(base,'snapshot.json'));
 return {manifest:path.relative(repo,path.join(base,'snapshot.json')).replaceAll('\\','/'),siteRoot:manifest.siteRoot,files:files.length};
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url))captureDocfx().then(r=>console.log(JSON.stringify(r,null,2))).catch(e=>{console.error(e);process.exitCode=1;});
