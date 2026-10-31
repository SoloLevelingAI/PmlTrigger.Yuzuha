import {spawn} from 'node:child_process';
import {mkdir,mkdtemp,writeFile,readFile} from 'node:fs/promises';
import {resolve,join,dirname} from 'node:path';
import {startBackend} from './support/backend-process.ts';
import {exampleProject} from '../packages/uic-core/example.ts';
import {exportUic} from '../packages/uic-core/index.ts';
const root=resolve('.runtime');await mkdir(root,{recursive:true});const fixture=await mkdtemp(join(root,'desktop-smoke-'));const install=join(fixture,'install');await mkdir(install);
const source=join(fixture,'DesktopFixture.uic'),mon=join(install,'mon.exe'),resultPath=join(fixture,'desktop-result.json');
await writeFile(source,await exportUic(await exampleProject()));await writeFile(mon,'Non-executable test fixture');
await writeFile(join(install,'DesignCustomization.xml'),'<UICustomizationSet><UICustomizationFiles><CustomizationFile Name="Existing" Path="'+source.replaceAll('&','&amp;')+'"/></UICustomizationFiles></UICustomizationSet>');
for(const module of ['Draft','Paragon','Verify'])await writeFile(join(install,module+'Customization.xml'),'<UICustomizationSet><UICustomizationFiles/></UICustomizationSet>');
const backend=await startBackend(join(fixture,'data'));
try{
 const child=spawn(process.env.UIC_DESKTOP_EXE??resolve('artifacts/desktop-workspace-0.10/RibbonStudio.Desktop.exe'),['--verify','--api',backend.url,'--source',source,'--mon',mon,'--result',resultPath],{windowsHide:true,stdio:['ignore','pipe','pipe']});
 let nativeLog="";child.stdout?.on("data",x=>nativeLog+=x);child.stderr?.on("data",x=>nativeLog+=x);
 const timer=setTimeout(()=>child.kill(),150000);
 const code=await new Promise<number|null>((resolve,reject)=>{child.once('exit',resolve);child.once('error',reject);});clearTimeout(timer);
 const result=await readFile(resultPath,'utf8').catch(()=>readFile(join(dirname(process.env.UIC_DESKTOP_EXE??resolve('artifacts/desktop-workspace-0.10/RibbonStudio.Desktop.exe')),'desktop-error.log'),'utf8')).catch(()=>nativeLog||'No native result file');
 if(code!==0)throw new Error('Desktop verification exit '+code+'\n'+result+'\nBackend: '+backend.log());
 console.log(result);console.log('Isolated evidence: '+fixture);
 const layoutFile=join(fixture,'restart-layout.json');
 for(const phase of ['seed','restore','docked']){
  const phaseResult=join(fixture,'layout-'+phase+'.json');
  const probe=spawn(process.env.UIC_DESKTOP_EXE??resolve('artifacts/desktop-workspace-0.10/RibbonStudio.Desktop.exe'),['--verify','--api',backend.url,'--layout-file',layoutFile,'--layout-phase',phase,'--result',phaseResult],{windowsHide:true,stdio:'ignore'});
  const timeout=setTimeout(()=>probe.kill(),90000);const status=await new Promise<number|null>((ok,bad)=>{probe.once('exit',ok);probe.once('error',bad);});clearTimeout(timeout);
  const proof=await readFile(phaseResult,'utf8').catch(()=>'No layout proof');if(status!==0)throw new Error('Layout restart '+phase+': '+proof);console.log(proof);
 }

}finally{await backend.close();}
