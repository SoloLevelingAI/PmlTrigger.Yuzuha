import http from 'node:http';
import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {generate,repositoryRoot,sha256} from './lib/contract-atlas-metadata.mjs';
import {createMethodBridge} from './lib/contract-style-methods.mjs';

const staticRoot=path.join(repositoryRoot,'docs/presentation/contract-style-lab');
const types={'.html':'text/html; charset=utf-8','.js':'text/javascript; charset=utf-8','.css':'text/css; charset=utf-8','.json':'application/json; charset=utf-8','.png':'image/png','.svg':'image/svg+xml','.webp':'image/webp'};
export function readContent(){
 const {publicData:metadata}=generate(repositoryRoot),pretty=value=>JSON.stringify(value,null,2)+'\n';
 const metadataDigest=sha256(pretty(metadata)),model=globalThis.ContractAtlasRender.build(metadata,{metadataDigest}),renderModelDigest=sha256(pretty(model));
 return {schema:'contract-style-lab/1',metadataDigest,renderModelDigest,metadata,model};
}
export async function createStyleLabServer({port=19440,pollMs=2000,root=repositoryRoot,outputRoot,contentReader=readContent}={}){
 const methods=createMethodBridge({root,outputRoot});
 let content=contentReader(),bytes=Buffer.from(JSON.stringify(content)),sourceError=null;
 const clients=new Set(),timers=new Set();
 const event=(name,value)=>{for(const res of clients)res.write(`event: ${name}\ndata: ${JSON.stringify(value)}\n\n`);};
 const poll=setInterval(()=>{try{const next=contentReader();if(next.metadataDigest!==content.metadataDigest||next.renderModelDigest!==content.renderModelDigest){content=next;bytes=Buffer.from(JSON.stringify(content));event('content',{metadataDigest:next.metadataDigest,renderModelDigest:next.renderModelDigest});}if(sourceError){sourceError=null;event('source-restored',{});}}catch(error){const message=String(error.message).slice(0,500);if(message!==sourceError){sourceError=message;event('source-error',{message});}}},pollMs);
 const server=http.createServer(async(req,res)=>{
  res.setHeader('Cache-Control','no-store');res.setHeader('X-Content-Type-Options','nosniff');
  res.setHeader('Content-Security-Policy',"default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-src 'self'; base-uri 'none'; form-action 'none'");
  const send=(code,value)=>{const body=Buffer.from(JSON.stringify(value));res.writeHead(code,{'Content-Type':types['.json'],'Content-Length':body.length});res.end(req.method==='HEAD'?undefined:body);};
  try{
   const actualPort=server.address().port;if(![`127.0.0.1:${actualPort}`,`localhost:${actualPort}`].includes(req.headers.host)){send(403,{error:'LOOPBACK_HOST_REQUIRED'});return;}
   if(!['GET','HEAD'].includes(req.method)){send(405,{error:'READ_ONLY_DESIGN_PREVIEW'});return;}
   const url=new URL(req.url,'http://'+req.headers.host),pathname=decodeURIComponent(url.pathname);
   if(pathname.includes('\\')||pathname.includes('\0')||pathname.split('/').includes('..')){send(403,{error:'INVALID_PATH'});return;}
   if(pathname==='/api/content'){res.setHeader('X-Design-Data-State',sourceError?'lastgood':'current');res.writeHead(200,{'Content-Type':types['.json'],'Content-Length':bytes.length});res.end(req.method==='HEAD'?undefined:bytes);return;}
   if(pathname==='/api/methods'){
    let current;try{current=contentReader();}catch{send(200,{schema:'contract-style-method-preview/1',status:'stale',metadataDigest:content.metadataDigest,renderModelDigest:content.renderModelDigest,revisionId:null,catalog:null,docfxUrls:{},reason:'Current public sources cannot be validated.'});return;}
    send(200,await methods.preview(current,{metadataDigest:url.searchParams.get('metadataDigest'),renderModelDigest:url.searchParams.get('renderModelDigest')}));return;
   }
   if(pathname.startsWith('/method-docfx/')){
    const [, ,revisionId,...parts]=pathname.split('/');let resource=null;try{resource=await methods.resource(contentReader(),revisionId,parts.join('/'));}catch{}
    const docTypes={...types,'.yml':'text/plain; charset=utf-8','.map':'application/json; charset=utf-8','.woff':'font/woff','.woff2':'font/woff2','.ico':'image/x-icon'};
    if(!resource||!docTypes[path.extname(resource.path)]){send(404,{error:'VERIFIED_DOCFX_UNAVAILABLE'});return;}
    // Serve the original hash-verified member document as a static reader. DocFX's
    // navigation scripts retry blocked TOC fetches indefinitely under an opaque origin.
    // Disabling these scripts preserves the exact member HTML/CSS without opening
    // parent-origin access, network connections, forms, or top-level navigation.
    res.setHeader('Content-Security-Policy',"sandbox; default-src 'self'; script-src 'none'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'none'; base-uri 'none'; form-action 'none'");
    res.writeHead(200,{'Content-Type':docTypes[path.extname(resource.path)],'Content-Length':resource.bytes.length});res.end(req.method==='HEAD'?undefined:resource.bytes);return;
   }
   if(pathname==='/api/events'&&req.method==='GET'){
    res.writeHead(200,{'Content-Type':'text/event-stream','Connection':'keep-alive'});clients.add(res);res.write(`event: content\ndata: ${JSON.stringify({metadataDigest:content.metadataDigest,renderModelDigest:content.renderModelDigest})}\n\n`);
    if(sourceError)res.write(`event: source-error\ndata: ${JSON.stringify({message:sourceError})}\n\n`);
    const timer=setInterval(()=>res.write(': keepalive\n\n'),15000);timers.add(timer);req.on('close',()=>{clients.delete(res);clearInterval(timer);timers.delete(timer);});return;
   }
   if(pathname.startsWith('/api/')){send(404,{error:'NO_DESIGN_WRITE_API'});return;}
   const aliases={'/shared/metadata-validator.js':path.join(repositoryRoot,'docs/presentation/contract-atlas/metadata-validator.js'),'/shared/render-model.js':path.join(repositoryRoot,'docs/presentation/contract-atlas/render-model.js'),'/shared/method-validator.js':path.join(repositoryRoot,'docs/presentation/contract-atlas/method-validator.js')};
   let target=aliases[pathname];if(!target){const relative=(pathname.endsWith('/')?pathname+'index.html':pathname).slice(1);target=await fs.realpath(path.join(staticRoot,relative));const inside=path.relative(await fs.realpath(staticRoot),target);if(path.isAbsolute(inside)||inside.startsWith('..'+path.sep)||inside==='..'){send(403,{error:'OUTSIDE_PREVIEW'});return;}}
   if(!types[path.extname(target)]){send(404,{error:'UNSUPPORTED_ASSET'});return;}
   const file=await fs.readFile(target);res.writeHead(200,{'Content-Type':types[path.extname(target)],'Content-Length':file.length});res.end(req.method==='HEAD'?undefined:file);
  }catch(error){send(error.code==='ENOENT'?404:422,{error:'DESIGN_RESOURCE_UNAVAILABLE',message:String(error.message).slice(0,300)});}
 });
 await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(port,'127.0.0.1',resolve);});
 return {server,url:`http://127.0.0.1:${server.address().port}/`,async close(){clearInterval(poll);for(const timer of timers)clearInterval(timer);for(const res of clients)res.end();await new Promise(resolve=>server.close(resolve));}};
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 const index=process.argv.indexOf('--port'),port=index<0?19440:Number(process.argv[index+1]);if(!Number.isInteger(port)||port<0||port>65535)throw Error('Invalid --port');
 const app=await createStyleLabServer({port});console.log('Contract Visual Lab: '+app.url+' (three independent concepts, shared read-only metadata)');
 const stop=async()=>{await app.close();process.exit(0);};process.once('SIGINT',stop);process.once('SIGTERM',stop);
}
