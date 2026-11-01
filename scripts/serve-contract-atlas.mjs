import http from 'node:http';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { ContractGeneration, insideFile } from './lib/contract-atlas-pipeline.mjs';
import { sha256, repositoryRoot } from './lib/contract-atlas-metadata.mjs';

const types={'.html':'text/html; charset=utf-8','.js':'text/javascript; charset=utf-8','.mjs':'text/javascript; charset=utf-8','.css':'text/css; charset=utf-8','.json':'application/json; charset=utf-8','.yml':'text/plain; charset=utf-8','.svg':'image/svg+xml','.png':'image/png','.jpg':'image/jpeg','.webp':'image/webp','.ico':'image/x-icon','.woff':'font/woff','.woff2':'font/woff2','.ttf':'font/ttf','.figcraft':'application/octet-stream'};
export async function createAtlasServer(options={}) {
  const staticRoot=path.join(options.assetRoot||repositoryRoot,'docs/presentation/contract-atlas');
  const coordinator=options.watch?await new ContractGeneration(options).start():null;
  const clients=new Set();
  function stateEvent(state){for(const response of clients)response.write(`event: state\ndata: ${JSON.stringify(state)}\n\n`);}
  coordinator?.on('state',stateEvent);
  const server=http.createServer(async(request,response)=>{
    response.setHeader('Cache-Control','no-store');response.setHeader('X-Content-Type-Options','nosniff');
    const send=(status,value)=>{const bytes=Buffer.from(JSON.stringify(value));response.writeHead(status,{'Content-Type':types['.json'],'Content-Length':bytes.length});response.end(bytes);};
    try {
      const port=server.address().port,host=request.headers.host;
      if(host!==`127.0.0.1:${port}`&&host!==`localhost:${port}`){send(403,{code:'HOST_REJECTED',message:'Loopback Host required'});return;}
      const origin='http://'+host,url=new URL(request.url,origin),name=decodeURIComponent(url.pathname);
      if(name.includes('\\')||name.includes('\0')){send(403,{code:'PATH_REJECTED'});return;}
      if(name.startsWith('/api/')){
        if(!coordinator){send(404,{code:'STATIC_ONLY',message:'Start --watch for live generation'});return;}
        if(request.headers.origin&&request.headers.origin!==origin){send(403,{code:'ORIGIN_REJECTED'});return;}
        if(request.method==='GET'&&name==='/api/state'){send(200,coordinator.state());return;}
        if(request.method==='GET'&&name==='/api/events'){
          response.writeHead(200,{'Content-Type':'text/event-stream','Connection':'keep-alive'});
          clients.add(response);response.write(`event: state\ndata: ${JSON.stringify(coordinator.state())}\n\n`);
          const heartbeat=setInterval(()=>response.write(': keepalive\n\n'),15000);
          request.on('close',()=>{clearInterval(heartbeat);clients.delete(response);});return;
        }
        if(request.method==='GET'&&name==='/api/authoring'){send(200,await coordinator.authoring());return;}
        if(request.method!=='POST'){send(405,{code:'METHOD_REJECTED'});return;}
        if(request.headers.origin!==origin||request.headers['x-atlas-token']!==coordinator.token){send(403,{code:'AUTHORING_TOKEN_REQUIRED'});return;}
        if(!coordinator.editEnabled&&name!=='/api/rebuild'){send(403,{code:'AUTHORING_DISABLED'});return;}
        if(!/^application\/json(?:;|$)/i.test(request.headers['content-type']||'')){send(415,{code:'JSON_REQUIRED'});return;}
        const chunks=[];let size=0;for await(const chunk of request){size+=chunk.length;if(size>2*1024*1024)throw Object.assign(new Error('Request exceeds 2 MiB'),{status:413,code:'INPUT_BUDGET'});chunks.push(chunk);}
        let input;try{input=JSON.parse(Buffer.concat(chunks).toString('utf8'));}catch{throw Object.assign(new Error('Invalid JSON'),{status:422,code:'INVALID_JSON'});}
        const fields=name==='/api/save'?['requestId','expectedSourceRevision','changes']:name==='/api/preview'?['expectedSourceRevision','changes']:name==='/api/rebuild'?[]:null;
        if(!fields){send(404,{code:'API_NOT_FOUND'});return;}
        if(!input||Array.isArray(input)||typeof input!=='object'||Object.keys(input).some(key=>!fields.includes(key))||fields.some(key=>!Object.hasOwn(input,key)))throw Object.assign(new Error('Unexpected or missing request field'),{status:422,code:'INVALID_INPUT'});
        if(name==='/api/preview')send(200,await coordinator.preview(input));
        else if(name==='/api/save')send(202,await coordinator.save(input));
        else send(202,await coordinator.requestBuild());
        return;
      }
      if(!['GET','HEAD'].includes(request.method)){send(405,{code:'METHOD_REJECTED'});return;}
      if(coordinator?.current&&name==='/') {response.writeHead(302,{Location:`/revisions/${coordinator.current.revisionId}/web/index.html`});response.end();return;}
      let root=staticRoot,relative=name==='/'?'index.html':name.slice(1),expected;
      if(name.startsWith('/revisions/')){
        if(!coordinator)throw Object.assign(new Error('No live revisions'),{status:404});
        const match=name.match(/^\/revisions\/([a-f0-9]{64})\/(.+)$/);if(!match)throw Object.assign(new Error('Invalid revision path'),{status:404});
        root=path.join(coordinator.outputRoot,'revisions',match[1]);relative=match[2];
        const manifest=JSON.parse(await fs.readFile(await insideFile(root,'manifest.json'),'utf8'));
        if(manifest.revisionId!==match[1])throw new Error('Manifest identity mismatch');
        if(relative!=='manifest.json'){expected=manifest.artifacts.find(item=>item.path===relative);if(!expected)throw Object.assign(new Error('Artifact is not published'),{status:404});}
      } else if(name.startsWith('/legacy/contracts/')) {root=path.join(options.assetRoot||repositoryRoot,'docs/presentation/contracts');relative=name.slice('/legacy/contracts/'.length)||'index.html';}
      if(relative==='manifest.json'&&!name.startsWith('/revisions/'))throw Object.assign(new Error('Not found'),{status:404});
      const actual=await insideFile(root,relative),bytes=await fs.readFile(actual);
      if(expected&&(bytes.length!==expected.bytes||sha256(bytes)!==expected.sha256))throw Object.assign(new Error('Published artifact hash mismatch'),{status:409,code:'ARTIFACT_CHANGED'});
      response.writeHead(200,{'Content-Type':types[path.extname(actual)]||'application/octet-stream','Content-Length':bytes.length});response.end(request.method==='HEAD'?undefined:bytes);
    } catch(e) {if(!response.headersSent)send(e.status||(/ENOENT|ENOTDIR/.test(e.code||'')?404:422),{code:e.code||'REQUEST_REJECTED',message:String(e.message).slice(0,1000)});else response.end();}
  });
  server.requestTimeout=30000;server.headersTimeout=15000;
  return {server,coordinator,async listen(port=0){await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(port,'127.0.0.1',resolve);});return `http://127.0.0.1:${server.address().port}/`;},async close(){coordinator?.off('state',stateEvent);for(const response of clients)response.end();await coordinator?.close();await new Promise(resolve=>server.close(resolve));}};
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
  const args=process.argv.slice(2),value=key=>{const index=args.indexOf(key);return index<0?undefined:args[index+1];};
  const port=Number(value('--port')||0);if(!Number.isInteger(port)||port<0||port>65535)throw Error('Invalid --port');
  const app=await createAtlasServer({root:value('--root'),outputRoot:value('--output-dir'),watch:args.includes('--watch'),edit:args.includes('--edit'),figcraftCli:value('--figcraft-cli')});
  const url=await app.listen(port);console.log('Contract Atlas: '+url+(app.coordinator?' (shared generation active)':' (static offline snapshot)'));
  const stop=async()=>{await app.close();process.exit(0);};process.once('SIGINT',stop);process.once('SIGTERM',stop);
}
