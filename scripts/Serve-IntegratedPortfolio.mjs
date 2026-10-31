import fs from 'node:fs';
import path from 'node:path';
import http from 'node:http';
import { fileURLToPath } from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const port=Number(process.argv[2]??4176);
if(!Number.isInteger(port)||port<0||port>65535)throw Error('Port must be 0..65535');
const types={'.html':'text/html; charset=utf-8','.css':'text/css; charset=utf-8','.js':'text/javascript; charset=utf-8','.mjs':'text/javascript; charset=utf-8','.json':'application/json','.png':'image/png','.jpg':'image/jpeg','.jpeg':'image/jpeg','.svg':'image/svg+xml','.woff':'font/woff','.woff2':'font/woff2','.ttf':'font/ttf','.md':'text/plain; charset=utf-8','.c4':'text/plain; charset=utf-8','.figcraft':'application/octet-stream'};
types['.ico']='image/x-icon';
for(const extension of ['.cs','.csproj','.props','.slnx','.xml','.txt','.py','.jsonl','.rsp','.toml','.yml','.yaml'])types[extension]='text/plain; charset=utf-8';
const server=http.createServer((request,response)=>{
  try{
    if(!['GET','HEAD'].includes(request.method)){response.writeHead(405).end();return}
    const name=decodeURIComponent(new URL(request.url,'http://127.0.0.1').pathname);
    let file=path.resolve(root,'.'+(name==='/'?'/integrations/index.html':name));
    if(!file.startsWith(root+path.sep)||!(/^[\\/]integrations[\\/]/.test(path.sep+path.relative(root,file))||file===path.join(root,'docs/reference/portable-development.md'))){response.writeHead(403).end();return}
    if(fs.existsSync(file)&&fs.statSync(file).isDirectory())file=path.join(file,'index.html');
    const type=types[path.extname(file).toLowerCase()];
    if(!type||!fs.existsSync(file)||!fs.statSync(file).isFile()){response.writeHead(404).end();return}
    const real=fs.realpathSync(file);if(!real.startsWith(fs.realpathSync(root)+path.sep)){response.writeHead(403).end();return}
    response.writeHead(200,{'Content-Type':type,'X-Content-Type-Options':'nosniff','Cache-Control':'no-store'});
    if(request.method==='HEAD')response.end();else fs.createReadStream(file).pipe(response);
  }catch{response.writeHead(400).end()}
});
server.listen(port,'127.0.0.1',()=>console.log(`PMLTWIN portfolio: http://127.0.0.1:${server.address().port}/`));
