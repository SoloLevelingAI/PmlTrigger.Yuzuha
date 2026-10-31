import {spawn} from 'node:child_process';
import {existsSync} from 'node:fs';
import {join,resolve} from 'node:path';
const root=resolve(import.meta.dirname,'..'),exe=join(root,'apps/backend/bin/RibbonStudio.Backend.exe');
if(!existsSync(exe))throw new Error('先运行 npm run backend:build');
const child=spawn(exe,process.argv.slice(2),{cwd:root,windowsHide:true,stdio:'inherit'});
for(const signal of ['SIGINT','SIGTERM'])process.on(signal,()=>{child.kill();process.exit();});
child.on('exit',code=>process.exit(code??1));child.on('error',error=>{console.error(error);process.exit(1);});
