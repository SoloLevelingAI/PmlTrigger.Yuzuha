import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,mkdir,writeFile,readFile} from 'node:fs/promises';
import {resolve,join} from 'node:path';
import {startBackend} from './support/backend-process.ts';
import {exampleProject} from '../packages/uic-core/example.ts';
import {exportUic} from '../packages/uic-core/index.ts';
test('registration scan deduplicates XML paths, reports actual installation metadata and persists profiles',async()=>{
 await mkdir(resolve('.runtime'),{recursive:true});const root=await mkdtemp(resolve('.runtime/registration-'));const dir=join(root,'Everything3D3.1');await mkdir(dir);const mon=join(dir,'mon.exe');await writeFile(mon,'non-executable fixture');const uic=join(root,'Shared.uic');await writeFile(uic,'fixture');
 const xml=join(dir,'DesignCustomization.xml');const content='<UICustomizationSet><UICustomizationFiles><CustomizationFile Name="A" Path="'+uic+'"/><CustomizationFile Name="Duplicate" Path="'+uic.toUpperCase()+'"/></UICustomizationFiles></UICustomizationSet>';await writeFile(xml,content);await writeFile(join(dir,'BadCustomization.xml'),'<broken/>');
 const data=join(root,'data');let backend=await startBackend(data);
 const post=async(route:string,body:any)=>{const r=await fetch(backend.url+'/api/v1/'+route,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});assert.equal(r.status,200,(await r.clone().text())+'\nBackend: '+backend.log());return r.json();};
 try{const scan=await post('registration/scan',{monPaths:[mon,mon.toUpperCase()]});const rows=scan.rows.filter((r:any)=>r.uicPath.toLowerCase()===uic.toLowerCase());assert.equal(rows.length,1);assert.equal(rows[0].product,'E3D');assert.equal(rows[0].version,'3.1');assert.equal(rows[0].versionSource,'安装目录名称');assert.equal(rows[0].xmlPath,xml);assert.equal(rows[0].origin,'既有自定义 / 未接管');assert(scan.errors.some((x:string)=>x.includes('BadCustomization.xml')));assert.equal(await readFile(xml,'utf8'),content);
 const profile=await post('registration/profile-save',{name:'可复用',monPath:mon,manifests:['DesignCustomization.xml']});await backend.close();backend=await startBackend(data);const all=await post('registration/profiles',{});assert.equal(all.profiles[0].id,profile.id);const fromSaved=await post('registration/scan',{});assert(fromSaved.rows.some((x:any)=>x.xmlPath===xml));
 }finally{await backend.close();}
});
test('multiple UIC files can join one XML with backups, without changing UIC bytes',async()=>{
 const root=await mkdtemp(resolve('.runtime/registration-write-'));const dir=join(root,'Marine12.1SP4');await mkdir(dir);const mon=join(dir,'mon.exe');await writeFile(mon,'fixture');const xml=join(dir,'DesignCustomization.xml');await writeFile(xml,'<UICustomizationSet><UICustomizationFiles/></UICustomizationSet>');
 const backend=await startBackend(join(root,'data'));const post=async(route:string,body:any)=>{const r=await fetch(backend.url+'/api/v1/'+route,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});assert.equal(r.status,200,(await r.clone().text())+'\nBackend: '+backend.log());return r.json();};
 try{const content=await exportUic(await exampleProject());let priorHash:string|undefined;for(const name of ['One','Two']){const path=join(root,name+'.uic');await writeFile(path,content);const {project}=await post('uic/import',{path});const p=await post('deployment/registration-preview',{project,monPath:mon,manifestName:'DesignCustomization.xml',entryName:name});if(priorHash)assert.equal(p.manifestHash,priorHash);const result=await post('deployment/registration-apply',{project,monPath:mon,manifestName:'DesignCustomization.xml',entryName:name,expectedManifestHash:p.manifestHash,expectedUicHash:p.uicHash});priorHash=result.nextManifestHash;assert.equal(await readFile(path,'utf8'),content);assert(await readFile(result.backup,'utf8'));}
 const scan=await post('registration/scan',{monPaths:[mon]});assert.equal(scan.rows.filter((r:any)=>r.xmlPath===xml).length,2);assert.match(await readFile(xml,'utf8'),/Name="One"/);assert.match(await readFile(xml,'utf8'),/Name="Two"/);
 }finally{await backend.close();}
});
