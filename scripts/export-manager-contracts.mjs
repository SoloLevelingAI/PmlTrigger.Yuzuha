import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const pretty=value=>JSON.stringify(value,null,2)+'\n';
export function schemaType(schema){
 if(schema.enum)return schema.enum.map(v=>JSON.stringify(v)).join(' | ');
 if(schema.anyOf)return schema.anyOf.map(schemaType).join(' | ');
 switch(schema.type){case 'string':return 'string';case 'integer':case 'number':return 'number';case 'boolean':return 'boolean';case 'null':return 'null';case 'array':return '('+schemaType(schema.items||{})+')[]';case 'object':{const required=new Set(schema.required||[]),properties=Object.entries(schema.properties||{});const body='{ '+properties.map(([key,value])=>JSON.stringify(key)+(required.has(key)?'':'?')+': '+schemaType(value)).join('; ')+' }';const additional=schema.additionalProperties;if(additional===true||additional&&typeof additional==='object'){const dictionary='Record<string, '+(additional===true?'JsonValue':schemaType(additional))+'>';return properties.length?body+' & '+dictionary:dictionary;}return body;}default:return 'unknown';}
}
export function artifacts(catalog){
 if(catalog?.journey?.schema!=='pmltwin.manager-journey/1'||!Array.isArray(catalog.operations))throw Error('Unsupported runtime contract export.');
 const ids=new Set(catalog.operations.map(op=>op.id));if(ids.size!==catalog.operations.length)throw Error('Duplicate registered operations.');
 for(const stage of catalog.journey.stages)for(const id of stage.operationIds)if(!ids.has(id))throw Error('Unregistered stage operation: '+id);
 const openapi={openapi:'3.1.0',info:{title:'PMLTWIN manager contract metadata (read only)',version:'1',description:'Only these GET catalog routes are HTTP APIs. Listed manager operations execute through the explicit CLI; no mutation HTTP endpoint is promised.'},servers:[{url:'http://127.0.0.1:5276'}],paths:{}};
 const text={type:'string'},strings={type:'array',items:text},reference=name=>({$ref:'#/components/schemas/'+name});
 const object=properties=>({type:'object',additionalProperties:false,properties,required:Object.keys(properties)});
 openapi.components={schemas:{
  ManagerContractScript:object(Object.fromEntries(['path','typeName','member','explanation','docFxPath','docFxUid'].map(key=>[key,text]))),
  ManagerContractStage:object({...Object.fromEntries(['id','title','input','precondition','method','effect','validation','receipt','recovery','evidenceBoundary'].map(key=>[key,text])),operationIds:strings,scripts:{type:'array',items:reference('ManagerContractScript')}}),
  ManagerJourneyContract:object({schema:{type:'string',const:'pmltwin.manager-journey/1'},id:text,title:text,summary:text,stages:{type:'array',items:reference('ManagerContractStage')}}),
  OperationDescriptor:object({id:text,contractVersion:{type:'integer',minimum:1},description:text,inputSchema:{type:'object',additionalProperties:true},requiresPid:{type:'boolean'},requiresExecution:{type:'boolean'},requiresWrite:{type:'boolean'},requiresLaunch:{type:'boolean'},legacyTools:{anyOf:[strings,{type:'null'}]}}),
  ManagerContractExport:object({journey:reference('ManagerJourneyContract'),operations:{type:'array',items:reference('OperationDescriptor')},outputBoundary:text}),
  OperationResult:{type:'object',description:'CLI envelope only. It is not an HTTP invocation route; data is arbitrary JSON and Int64 values must be preserved from raw CLI text.',properties:{operationId:text,success:{type:'boolean'},exitCode:{type:'integer'},schemaVersion:{type:'integer'},data:true,error:{anyOf:[object({code:text,message:text,mayHaveExecuted:{type:'boolean'}}),{type:'null'}]}},required:['operationId','success','exitCode','schemaVersion']}
 }};
 for(const [route,id,description]of [['/api/manager/contracts','GetManagerContracts','Complete seven-field journey and actual registry.'],['/api/manager/operations','GetManagerOperations','Actual input schemas and explicit grant requirements.'],['/api/manager/operations/{id}','GetManagerOperation','One actual registered operation; unknown IDs return 404.']]){
  const response=route.endsWith('/contracts')?reference('ManagerContractExport'):route.endsWith('/operations')?{type:'array',items:reference('OperationDescriptor')}:reference('OperationDescriptor');
  const get={operationId:id,description,responses:{'200':{description:'Read-only metadata; OperationResult.data for CLI invocation remains unknown JSON.',content:{'application/json':{schema:response}}}}};
  if(route.includes('{id}')){get.parameters=[{name:'id',in:'path',required:true,schema:{type:'string',enum:[...ids]}}];get.responses['404']={description:'Unknown operation.'};}
  openapi.paths[route]={get};
 }
 openapi['x-cli-input-schemas']=Object.fromEntries(catalog.operations.map(operation=>[operation.id,operation.inputSchema]));
 const types='// Generated from the actual registered input schemas. Do not edit.\n'+
  'import {invokeCli as transport} from "./manager-journey-client.mjs";\n'+
  'export type JsonValue = null | boolean | number | string | JsonValue[] | { [key: string]: JsonValue };\n'+
  'export interface OperationResult { operationId: string; success: boolean; exitCode: number; data?: unknown; error?: { code: string; message: string; mayHaveExecuted?: boolean }; schemaVersion: number }\n'+
  'export interface CliContext { processId?: number; expectedModel?: string; expectedProcessStartTimeUtcTicks?: string; allowWrite?: boolean; allowLaunch?: boolean; allowExecution?: boolean }\n'+
  'export interface CliOptions { executable: string; prefixArgs?: string[]; context?: CliContext; timeoutMs?: number; maximumOutputBytes?: number }\n'+
  'export interface CliReply { exitCode: number | null; signal: string | null; rawJson: string; stderr: string; mayHaveExecuted: boolean }\n'+
  'export interface ManagerInputs {\n'+catalog.operations.map(op=>'  '+JSON.stringify(op.id)+': '+schemaType(op.inputSchema)+';').join('\n')+'\n}\n'+
  'export type ManagerOperationId = keyof ManagerInputs;\n'+
  '/** Pass credentials via stdin. Keep Int64 ticks as decimal strings; rawJson is deliberately not parsed or retried. */\n'+
  'export function invokeCli<Id extends ManagerOperationId>(operation: Id, input: ManagerInputs[Id], options: CliOptions): Promise<CliReply> { return transport(operation,input,options); }\n';
 const declaration='import type {CliOptions,CliReply} from "./manager-journey-client.js";\nexport declare function invokeCli(operation: string,input: unknown,options: CliOptions): Promise<CliReply>;\n';
 return new Map([['manager-journey.json',pretty(catalog)],['manager-journey.openapi.json',pretty(openapi)],['manager-journey-client.ts',types],['manager-journey-client.mjs',fs.readFileSync(path.join(root,'scripts/lib/manager-cli-client.mjs'),'utf8')],['manager-journey-client.d.mts',declaration]]);
}

if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 const args=process.argv.slice(2),option=name=>{const index=args.indexOf(name);return index>=0?args[index+1]:null;};
 let catalog;const from=option('--from');
 if(from)catalog=JSON.parse(fs.readFileSync(path.resolve(from),'utf8').replace(/^\uFEFF/,''));
 else{
  const cli=path.resolve(option('--manager-cli')||path.join(root,'src/YuzuhaToolkit.Manager.Cli/bin/Release/net10.0/Yuzuha.Manager.Cli.dll'));
  if(!fs.existsSync(cli))throw Error('Build the manager CLI first: dotnet build src/YuzuhaToolkit.Manager.Cli -c Release');
  const dotnet=option('--dotnet')||process.env.DOTNET_HOST_PATH||'dotnet';
  const result=spawnSync(dotnet,[cli,'op','invoke','manager.contracts','--input-stdin'],{cwd:root,input:'{}',encoding:'utf8',windowsHide:true,maxBuffer:8*1024*1024,timeout:30000});
  if(result.error||result.status!==0)throw Error('Read-only manager contract export failed; build the current CLI and inspect its JSON error.');
  const envelope=JSON.parse(result.stdout);if(!envelope.success)throw Error('Manager contract export rejected.');catalog=envelope.data;
 }
 const destination=path.resolve(option('--output')||path.join(root,'docs/contracts')),check=args.includes('--check');if(!check)fs.mkdirSync(destination,{recursive:true});
 for(const [name,bytes]of artifacts(catalog)){const target=path.join(destination,name);if(check){if(!fs.existsSync(target)||fs.readFileSync(target,'utf8')!==bytes)throw Error('Manager contract artifact drift: '+name);}else fs.writeFileSync(target,bytes);}
 console.log(JSON.stringify({mode:check?'check':'build',stages:catalog.journey.stages.length,operations:catalog.operations.length,files:5}));
}
