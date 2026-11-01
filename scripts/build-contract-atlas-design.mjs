import fs from 'node:fs/promises';
import path from 'node:path';
import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {createHash,randomUUID} from 'node:crypto';
import '../docs/presentation/contract-atlas/metadata-validator.js';
import '../docs/presentation/contract-atlas/render-model.js';
import '../docs/design/contract-atlas/source/adapter-core.js';
import {compileNative} from '../docs/design/contract-atlas/source/native-commands.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.join(root,'docs/design/contract-atlas');
const encode=value=>JSON.stringify(value,null,2)+'\n',sha=bytes=>createHash('sha256').update(bytes).digest('hex');
export const designDigest=value=>sha(encode(value));
const files=async directory=>{const result=[];for(const entry of await fs.readdir(directory,{withFileTypes:true})){const p=path.join(directory,entry.name);if(entry.isDirectory())result.push(...await files(p));else if(entry.isFile())result.push(p);else throw Error('Non-regular build artifact');}return result;};
async function cliRun(executable,args,{timeoutMs=120000,maxBytes=20*1024*1024}={}){
 return new Promise((resolve,reject)=>{
  const child=spawn(executable,args,{windowsHide:true,stdio:['ignore','pipe','pipe']});const stdoutChunks=[],stderrChunks=[];let bytes=0,failed=false;
  const fail=error=>{if(failed)return;failed=true;clearTimeout(timer);child.kill();reject(error);};
  const timer=setTimeout(()=>fail(Object.assign(Error('FIGCRAFT_TIMEOUT: isolated staging left unpublished'),{code:'FIGCRAFT_TIMEOUT'})),timeoutMs);
  const collect=key=>chunk=>{bytes+=chunk.length;if(bytes>maxBytes)return fail(Object.assign(Error('FIGCRAFT_OUTPUT_LIMIT'),{code:'FIGCRAFT_OUTPUT_LIMIT'}));(key==='stdout'?stdoutChunks:stderrChunks).push(chunk);};
  child.stdout.on('data',collect('stdout'));child.stderr.on('data',collect('stderr'));child.on('error',fail);
  child.on('close',code=>{if(failed)return;clearTimeout(timer);const stdout=Buffer.concat(stdoutChunks).toString('utf8'),stderr=Buffer.concat(stderrChunks).toString('utf8');if(code!==0)return reject(Object.assign(Error('FIGCRAFT_FAILED: '+stderr.slice(0,1200)),{code:'FIGCRAFT_FAILED',stdout,stderr}));resolve({stdout,stderr});});
 });
}
export function verifyNative(native,compiled,renderModel,{metadataDigest,renderModelDigest,tokens}){
 const nodes=Object.values(native.nodes||{}),instances=nodes.filter(n=>n.type==='instance');
 for(const card of compiled.model.all){const found=instances.find(n=>n.plugin_data?.metadataId===card.id&&n.props?.Title===card.title&&n.props?.Body===card.body&&n.props?.Status===card.status&&n.props?.Limit===card.limit);if(!found)throw Error('NATIVE_CARD_MISMATCH: '+card.id);}
 for(const instance of instances){if(!native.nodes[instance.component]||instance.plugin_data.metadataDigest!==metadataDigest||instance.plugin_data.renderModelDigest!==renderModelDigest)throw Error('NATIVE_PROVENANCE_MISMATCH');}
 for(const flow of renderModel.flows){for(const step of flow.steps){const box=nodes.find(n=>n.plugin_data?.flowId===flow.id&&n.plugin_data?.stepId===step.id),children=(box?.children||[]).map(id=>native.nodes[id]?.characters);if(!box||!children.includes(step.title)||!children.includes(step.summary))throw Error('NATIVE_STEP_MISMATCH: '+step.id);}for(const [index,c]of flow.connections.entries()){const match=n=>n.plugin_data?.flowId===flow.id&&n.plugin_data.from===c.from&&n.plugin_data.to===c.to&&n.plugin_data.label===c.label&&n.plugin_data.kind===c.kind;if(!nodes.some(n=>match(n)&&n.characters===(index+1)+'. '+c.from+' → '+c.to+' / '+c.label+' ['+c.kind+']')||!nodes.some(n=>match(n)&&n.type==='vector'&&[true,'true'].includes(n.plugin_data.connectionVector)))throw Error('NATIVE_CONNECTION_MISMATCH: '+flow.id);}}
 const modes=native.variables?.collections?.find(c=>c.name==='Atlas')?.modes?.map(m=>m.name);if(JSON.stringify(modes)!=='["Light","Dark"]')throw Error('NATIVE_COLOR_MODES_INVALID');
 if((native.imageIndex||[]).length)throw Error('NATIVE_RASTER_SUBSTITUTION');
 if(compiled.taskLayout){
  const layout=compiled.taskLayout;
  for(const theme of ['Light','Dark']){const board=nodes.find(n=>n.name==='TaskMap-'+theme&&n.type==='frame');if(!board||board.plugin_data?.taskMapSchema!==renderModel.taskMap.schema)throw Error('NATIVE_TASK_MAP_MISSING');const children=nodes.filter(n=>n.parent===board.id);
   for(const box of layout.boxes){const frame=children.find(n=>n.plugin_data?.taskMapKey===box.key),texts=(frame?.children||[]).map(id=>native.nodes[id]?.characters);if(!frame||!texts.includes(box.node?.title||box.title)||!texts.includes(box.body))throw Error('NATIVE_TASK_MAP_FACT_MISMATCH: '+box.key);}
   const relations=children.filter(n=>n.type==='vector'&&n.plugin_data?.taskMapPart==='relation');if(relations.length!==layout.routes.length)throw Error('NATIVE_TASK_MAP_RELATION_COUNT');for(const route of layout.routes){const edge=relations.find(n=>n.plugin_data.from===route.from&&n.plugin_data.to===route.to&&n.plugin_data.kind===route.kind&&n.plugin_data.label===route.label);if(!edge||String(edge.plugin_data.arrow)!==String(route.arrow))throw Error('NATIVE_TASK_MAP_RELATION_MISMATCH');}
   for(const row of layout.detailRows)if(!children.some(n=>n.type==='text'&&n.plugin_data?.taskMapPart==='selected-fact'&&n.plugin_data.label===row.label&&n.characters===row.text))throw Error('NATIVE_TASK_MAP_DETAIL_MISMATCH');
  }
 }
 if(tokens){
  const variables=native.variables.variables,variable=name=>variables.find(v=>v.name===name);
  for(const [role,section]of Object.entries(tokens.sections))for(const field of ['accent','surface']){const v=variable('section-'+role+'-'+field);if(!v)throw Error('NATIVE_SECTION_VARIABLE_MISSING');for(const [i,theme]of ['light','dark'].entries()){const actual=Object.values(v.values)[i]?.value,hex=section[theme][field];if(!actual||['r','g','b'].some((channel,index)=>Math.abs(actual[channel]-parseInt(hex.slice(1+index*2,3+index*2),16)/255)>0.00001))throw Error('NATIVE_SECTION_COLOR_MISMATCH');}}
  for(const [role,t]of Object.entries(tokens.typography))for(const [field,value]of Object.entries(t)){const v=variable('type-'+role+'-'+field);if(!v||Object.values(v.values).some(v=>v.value!==value))throw Error('NATIVE_TYPOGRAPHY_VARIABLE_MISMATCH');}
  const script=renderModel.cards.find(c=>c.entityId==='script:launch-workflow');
  for(const theme of ['Light','Dark']){const board=nodes.find(n=>n.name==='Document-'+theme&&n.type==='frame');if(!board)throw Error('NATIVE_DOCUMENT_MISSING');const descendants=nodes.filter(n=>n.parent===board.id);if(descendants.filter(n=>n.plugin_data?.documentPart==='directory').length!==renderModel.coverage.features+renderModel.coverage.scripts)throw Error('NATIVE_DOCUMENT_DIRECTORY_MISMATCH');const header=descendants.find(n=>n.plugin_data?.documentPart==='task-header'),headerTexts=(header?.children||[]).map(id=>native.nodes[id]);if(!header||!headerTexts.some(n=>n.plugin_data?.documentPart==='goal-heading'&&n.characters==='任务目标'&&n.style?.font_size===18)||!descendants.some(n=>n.plugin_data?.documentPart==='boundary-heading'&&n.characters==='适用边界'&&n.style?.font_size===18)||!headerTexts.some(n=>n.plugin_data?.documentPart==='value'&&n.characters===(renderModel.cards.find(c=>c.entityId===script.featureIds?.[0])?.summary||script.summary))||descendants.filter(n=>n.plugin_data?.documentPart==='selected-directory'&&n.plugin_data.metadataId===script.entityId).length!==1)throw Error('NATIVE_DOCUMENT_HIERARCHY_MISMATCH');for(const section of script.sections){const box=descendants.find(n=>n.plugin_data?.sectionId===section.id&&n.plugin_data.documentPart==='contract-field'),texts=(box?.children||[]).map(id=>native.nodes[id]);if(!box||box.strokes?.length||!texts.some(n=>n.characters===section.text&&n.style?.font_size===tokens.typography.body.size))throw Error('NATIVE_DOCUMENT_FIELD_MISMATCH');}for(const role of Object.keys(tokens.sections))if(!descendants.some(n=>n.plugin_data?.sectionRole===role&&n.plugin_data.documentPart==='section-example'&&n.height<=40&&n.width<=192))throw Error('NATIVE_DOCUMENT_SECTION_MISSING');}
 }
 return {generated:true,validated:true,success:true,components:nodes.filter(n=>n.type==='component').length,instances:instances.length,editableTexts:nodes.filter(n=>n.type==='text').length,variables:native.variables.variables.length,images:0};
}
/** Generates only into a new directory. The coordinator owns publication/lastgood. */
export async function buildDesign({metadata,metadataDigest,renderModel,outputDir,revisionId=randomUUID(),figcraftCli,tokens,render=true}){
 if(!outputDir||!path.isAbsolute(outputDir))throw Error('OUTPUT_DIRECTORY_REQUIRED: supply an absolute new directory');
 if(typeof render!=='boolean')throw Error('INVALID_RENDER_FLAG');
 metadata=JSON.parse(encode(metadata));renderModel=JSON.parse(encode(renderModel));
 const actualDigest=designDigest(metadata);if(metadataDigest&&metadataDigest!==actualDigest)throw Error('METADATA_DIGEST_MISMATCH');metadataDigest=actualDigest;
 globalThis.ContractAtlasDesign.render(metadata,renderModel,metadataDigest);
 const renderModelDigest=designDigest(renderModel);
 tokens=tokens?JSON.parse(encode(tokens)):JSON.parse(await fs.readFile(path.join(root,'docs/presentation/contract-atlas/tokens.json'),'utf8'));
 globalThis.ContractAtlasDesign.themes(tokens);
 if(typeof revisionId!=='string'||!/^[A-Za-z0-9_-]{1,160}$/.test(revisionId))throw Error('INVALID_REVISION_ID');
 const compiled=compileNative({metadata,renderModel,tokens,metadataDigest,renderModelDigest,revisionId});
 // mkdir without recursive is the ownership gate: existing or hand-authored dirs are refused.
 await fs.mkdir(outputDir);let phase='prepare';
 const write=async(p,bytes)=>{const target=path.join(outputDir,p);await fs.mkdir(path.dirname(target),{recursive:true});await fs.writeFile(target,bytes,{flag:'wx'});};
 try{
  await write('metadata.json',encode(metadata));await write('render-model.json',encode(renderModel));await write('tokens.json',encode(tokens));
  await write('generated/commands.json',encode(compiled.commands));await write('generated/entity-map.json',encode(compiled.map));
  const bundled=['source/figma-globals.js',null,'source/adapter-core.js','source/figma-runtime.js'];
  const validator=await fs.readFile(path.join(root,'docs/presentation/contract-atlas/metadata-validator.js'),'utf8'),sharedModel=await fs.readFile(path.join(root,'docs/presentation/contract-atlas/render-model.js'),'utf8');
  const code=await fs.readFile(path.join(source,bundled[0]),'utf8')+'\n'+validator+'\n'+sharedModel+'\n'+await fs.readFile(path.join(source,bundled[2]),'utf8')+'\nglobalThis.ContractAtlasTokens='+JSON.stringify(tokens)+';\n'+await fs.readFile(path.join(source,bundled[3]),'utf8');
  await write('figma-plugin/code.js',code);for(const name of ['ui.html','manifest.json'])await write('figma-plugin/'+name,await fs.readFile(path.join(source,'figma-plugin',name)));
  let native={generated:false,validated:false,success:false,status:'not-run'},toolVersion=null;
  if(render){
   const executable=figcraftCli||process.env.FIGCRAFT_CLI||path.join(process.env.LOCALAPPDATA||'','Programs/FigCraft/versions/0.6.0/figcraft-cli.exe');
   try{await fs.access(executable);}catch{throw Object.assign(Error('FIGCRAFT_UNAVAILABLE: no native build was published'),{code:'FIGCRAFT_UNAVAILABLE'});}
   phase='tool-version';toolVersion=(await cliRun(executable,['--version'],{timeoutMs:10000})).stdout.trim();
   phase='native-generate';const run=await cliRun(executable,['run',path.join(outputDir,'generated/commands.json'),'--out',path.join(outputDir,'contract-atlas.figcraft')]);await write('generated/cli-run.jsonl',run.stdout);
   const results=run.stdout.trim().split('\n').map(line=>JSON.parse(line));if(results.length!==compiled.commands.length||results.some(r=>r.ok!==true))throw Error('NATIVE_COMMAND_REJECTED');
   phase='native-render';for(const board of compiled.boards){const scale=Math.min(1,12000/board.height);try{await cliRun(executable,['export',path.join(outputDir,'contract-atlas.figcraft'),'-o',path.join(outputDir,'generated',board.name.toLowerCase()+'.png'),'--node',String(board.id),'--scale',String(scale)]);}catch(error){error.message='Preview '+board.name+' (scale '+scale+'): '+error.message;throw error;}}
   phase='native-convert';await cliRun(executable,['convert',path.join(outputDir,'contract-atlas.figcraft'),path.join(outputDir,'generated/native.json')]);
   phase='native-validate';native=verifyNative(JSON.parse(await fs.readFile(path.join(outputDir,'generated/native.json'),'utf8')),compiled,renderModel,{metadataDigest,renderModelDigest,tokens});
  }
  phase='receipt';const artifacts=[];for(const file of (await files(outputDir)).sort()){const bytes=await fs.readFile(file),relative=path.relative(outputDir,file).replaceAll('\\','/');artifacts.push({path:relative,sha256:sha(bytes),bytes:bytes.length,role:relative.endsWith('.figcraft')?'figcraft':relative==='generated/native.json'?'native-structure':relative.startsWith('figma-plugin/')?'figma-plugin':'design'});}
  const receipt={schema:'contract-atlas-design-build/1',revisionId,metadataDigest,renderModelDigest,tokenDigest:designDigest(tokens),native:{...native,toolVersion,commandCount:compiled.commands.length},figma:{execution:'not-run',validation:'offline mock only'},artifacts};
  await write('build-receipt.json',encode(receipt));const receiptBytes=await fs.readFile(path.join(outputDir,'build-receipt.json'));
  return {outputDir,...receipt,artifacts:[...artifacts,{path:'build-receipt.json',sha256:sha(receiptBytes),bytes:receiptBytes.length,role:'design-receipt'}]};
 }catch(error){await fs.writeFile(path.join(outputDir,'failure.json'),encode({schema:'contract-atlas-design-failure/1',revisionId,metadataDigest,renderModelDigest,phase,code:error.code||'DESIGN_BUILD_FAILED',message:String(error.message||error).slice(0,2000),published:false})).catch(()=>{});throw error;}
}
async function main(){
 const args=process.argv.slice(2),value=key=>{const i=args.indexOf(key);if(i<0)return null;if(!args[i+1]||args[i+1].startsWith('--'))throw Error('Missing '+key+' value');return args[i+1];};
 for(const arg of args.filter(a=>a.startsWith('--')))if(!['--input','--output','--render-model','--check','--commands-only'].includes(arg))throw Error('Unknown option '+arg);
 if(args.includes('--check')){const directory=value('--output');if(!directory)throw Error('--check requires --output of an immutable build');const receipt=JSON.parse(await fs.readFile(path.join(directory,'build-receipt.json'),'utf8'));for(const artifact of receipt.artifacts){if(sha(await fs.readFile(path.join(directory,artifact.path)))!==artifact.sha256)throw Error('ARTIFACT_CHANGED: '+artifact.path);}console.log(encode({checked:true,metadataDigest:receipt.metadataDigest,renderModelDigest:receipt.renderModelDigest,native:receipt.native}));return;}
 const input=path.resolve(value('--input')||path.join(root,'docs/contracts/project.public.json')),metadata=JSON.parse(await fs.readFile(input,'utf8')),metadataDigest=designDigest(metadata);
 const irPath=value('--render-model'),renderModel=irPath?JSON.parse(await fs.readFile(path.resolve(irPath),'utf8')):globalThis.ContractAtlasRender.build(metadata,{metadataDigest});
 let output=value('--output');if(!output){const parent=path.join(root,'artifacts/contract-atlas-design');await fs.mkdir(parent,{recursive:true});output=path.join(parent,randomUUID());process.stderr.write('Default build now writes a NEW isolated directory; docs/design and hand-edited files are preserved. See outputDir in the receipt.\n');}
 const receipt=await buildDesign({metadata,metadataDigest,renderModel,outputDir:path.resolve(output),render:!args.includes('--commands-only')});console.log(encode(receipt));
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url))main().catch(error=>{console.error(error.message);process.exitCode=1;});
