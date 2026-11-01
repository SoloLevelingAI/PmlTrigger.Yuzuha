import {spawn} from 'node:child_process';

// Preserve raw JSON so Int64 process identities cannot be rounded by JSON.parse.
export function invokeCli(operation,input,{executable,prefixArgs=[],context={},timeoutMs=120000,maximumOutputBytes=8*1024*1024}={}){
 if(typeof executable!=='string'||!executable||!Array.isArray(prefixArgs)||prefixArgs.some(a=>typeof a!=='string'))throw Error('Explicit CLI executable and argument array are required.');
 if(!/^[a-z][a-z0-9]*(\.[a-z][a-z0-9-]*)+$/.test(operation))throw Error('Invalid operation ID.');
 if(!Number.isInteger(timeoutMs)||timeoutMs<1||timeoutMs>3600000||!Number.isInteger(maximumOutputBytes)||maximumOutputBytes<1||maximumOutputBytes>16*1024*1024)throw Error('Invalid transport budget.');
 const args=[...prefixArgs,'op','invoke',operation,'--input-stdin'];
 if(context.processId!=null){if(!Number.isSafeInteger(context.processId)||context.processId<=0)throw Error('PID must be a positive safe integer.');args.push('--pid',String(context.processId));}
 if(context.expectedModel!=null){if(typeof context.expectedModel!=='string'||!context.expectedModel.trim())throw Error('Expected model must be explicit text.');args.push('--expected-model',context.expectedModel);}
 if(context.expectedProcessStartTimeUtcTicks!=null){const ticks=context.expectedProcessStartTimeUtcTicks;if(typeof ticks!=='string'||! /^[1-9][0-9]{0,18}$/.test(ticks)||BigInt(ticks)>9223372036854775807n||context.processId==null)throw Error('Process ticks must be an explicit positive Int64 decimal string with PID.');args.push('--expected-start-time-utc-ticks',ticks);}
 for(const [key,flag]of [['allowWrite','--allow-write'],['allowLaunch','--allow-launch'],['allowExecution','--allow-execution']]){if(context[key]!=null&&typeof context[key]!=='boolean')throw Error('Grant flags must be explicit booleans.');if(context[key]===true)args.push(flag);}
 const body=JSON.stringify(input);if(typeof body!=='string'||Buffer.byteLength(body)>1024*1024)throw Error('JSON input exceeds the transport budget.');
 return new Promise((resolve,reject)=>{
  const child=spawn(executable,args,{shell:false,windowsHide:true,stdio:['pipe','pipe','pipe']}),stdout=[],stderr=[];let bytes=0,finished=false;
  // Even a preview can persist a non-secret plan. Once a child starts, grants
  // alone cannot prove that no handler ran; leave outcome interpretation to its raw envelope.
  const fail=(message,mayHaveExecuted=true)=>{if(finished)return;finished=true;clearTimeout(timer);child.kill();const error=Object.assign(new Error(message),{mayHaveExecuted});reject(error);};
  const timer=setTimeout(()=>fail('CLI transport timed out; inspect the original operation, never retry automatically.'),timeoutMs);
  for(const [stream,chunks]of [[child.stdout,stdout],[child.stderr,stderr]])stream.on('data',value=>{bytes+=value.length;if(bytes>maximumOutputBytes)fail('CLI output budget exceeded; inspect the original operation.');else chunks.push(value);});
  child.on('error',()=>fail('CLI process could not be started.',false));child.stdin.on('error',()=>{});
  child.on('close',(code,signal)=>{if(finished)return;finished=true;clearTimeout(timer);resolve({exitCode:code,signal,rawJson:Buffer.concat(stdout).toString('utf8'),stderr:Buffer.concat(stderr).toString('utf8'),mayHaveExecuted:code!==0});});
  child.stdin.end(body,'utf8');
 });
}
