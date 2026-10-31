import {spawnSync} from 'node:child_process';
import {mkdirSync,readdirSync,copyFileSync} from 'node:fs';
import {dirname,join,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'..'), source=join(root,'apps/desktop'), output=process.env.UIC_DESKTOP_OUTPUT?resolve(process.env.UIC_DESKTOP_OUTPUT):join(root,'artifacts','desktop-workspace-0.10');
mkdirSync(join(output,'Resources'),{recursive:true});
for(const file of readdirSync(join(source,'Resources'))) copyFileSync(join(source,'Resources',file),join(output,'Resources',file));
const framework=join(process.env.WINDIR??'C:/Windows','Microsoft.NET/Framework64/v4.0.30319');
function sources(folder){return readdirSync(folder,{withFileTypes:true}).flatMap(item=>item.isDirectory()?['bin','obj'].includes(item.name)?[]:sources(join(folder,item.name)):item.name.endsWith('.cs')?[join(folder,item.name)]:[]);}
const references=['System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Net.Http.dll','System.Web.Extensions.dll','WPF/PresentationFramework.dll','WPF/PresentationCore.dll','WPF/WindowsBase.dll','System.Xaml.dll'];
const result=spawnSync(join(framework,'csc.exe'),['/nologo','/target:winexe','/optimize+','/debug:pdbonly','/out:'+join(output,'RibbonStudio.Desktop.exe'),...references.map(file=>'/reference:'+join(framework,file)),...sources(join(root,'packages/contracts-csharp')),...sources(source)],{encoding:'utf8',windowsHide:true});
process.stdout.write(result.stdout??'');process.stderr.write(result.stderr??'');
if(result.error)throw result.error;if(result.status!==0)process.exit(result.status??1);
copyFileSync(join(source,'App.config'),join(output,'RibbonStudio.Desktop.exe.config'));
console.log('Desktop build: '+join(output,'RibbonStudio.Desktop.exe'));
