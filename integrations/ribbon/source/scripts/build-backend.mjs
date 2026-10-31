import {spawnSync} from 'node:child_process';
import {mkdirSync,readdirSync,copyFileSync} from 'node:fs';
import {resolve,join} from 'node:path';
const root=resolve(import.meta.dirname,'..'),source=join(root,'apps/backend'),output=process.env.UIC_BACKEND_OUTPUT?resolve(process.env.UIC_BACKEND_OUTPUT):join(source,'bin');
mkdirSync(join(output,'Assets'),{recursive:true});
for(const file of readdirSync(join(source,'Assets')))copyFileSync(join(source,'Assets',file),join(output,'Assets',file));
const framework=join(process.env.WINDIR??'C:/Windows','Microsoft.NET/Framework64/v4.0.30319');
function sources(folder){return readdirSync(folder,{withFileTypes:true}).flatMap(item=>item.isDirectory()?['bin','obj'].includes(item.name)?[]:sources(join(folder,item.name)):item.name.endsWith('.cs')?[join(folder,item.name)]:[]);}
const references=['System.Core.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Xml.dll','System.Xml.Linq.dll'];
const result=spawnSync(join(framework,'csc.exe'),['/nologo','/win32manifest:'+join(source,'app.manifest'),'/target:exe','/optimize+','/debug:pdbonly','/out:'+join(output,'RibbonStudio.Backend.exe'),...references.map(file=>'/reference:'+join(framework,file)),...sources(join(root,'packages/contracts-csharp')),...sources(source)],{encoding:'utf8',windowsHide:true});
process.stdout.write(result.stdout??'');process.stderr.write(result.stderr??'');if(result.error)throw result.error;if(result.status!==0)process.exit(result.status??1);console.log('Backend build: '+join(output,'RibbonStudio.Backend.exe'));

copyFileSync(join(source,'App.config'),join(output,'RibbonStudio.Backend.exe.config'));
