import {execFileSync} from 'node:child_process';
import {readFileSync,mkdirSync} from 'node:fs';
import {resolve,dirname,join} from 'node:path';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';

const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
const baseline=process.argv[2]??'42eb10d';
const output=join(root,'artifacts/comment-compression');mkdirSync(output,{recursive:true});
const inventory=(revision,name)=>{
    const path=join(output,name+'.json');
    execFileSync('dotnet',['run','--project','tools/documentation/DocumentationChecks.csproj','--','--comments',root,path,...(revision?[revision]:[])],{cwd:root,stdio:'pipe'});
    return JSON.parse(readFileSync(path,'utf8'));
};
const before=inventory(baseline,'before'),after=inventory(null,'after');
const count=text=>[...text.replace(/\s/g,'')].length;
const measure=data=>({files:data.files.length,body:data.files.reduce((sum,file)=>sum+file.comments.reduce((sum,comment)=>sum+count(comment.body),0),0)+(data.sharedSources??[]).reduce((sum,source)=>sum+count(source.body),0),
    withMarkup:data.files.reduce((sum,file)=>sum+file.comments.reduce((sum,comment)=>sum+count(comment.raw),0),0)+(data.sharedSources??[]).reduce((sum,source)=>sum+count(source.raw),0)});
const left=new Map(before.files.map(file=>[file.file,file.codeHash]));
assert.equal(after.files.length,left.size,'C# file set changed');
for(const file of after.files)assert.equal(file.codeHash,left.get(file.file),'Executable code changed: '+file.file);
const original=measure(before),current=measure(after);
const result={baseline,scope:'All rewrite C# XML and ordinary comment prose, plus every shared XML definition exactly once. Unicode non-whitespace characters; markup and include-path costs reported separately.',
    before:original,after:current,bodyReductionPercent:(1-current.body/original.body)*100,withMarkupReductionPercent:(1-current.withMarkup/original.withMarkup)*100,
    codeTokenHashesUnchanged:after.files.length,sharedDefinitions:(after.sharedSources??[]).reduce((sum,source)=>sum+source.definitions,0),resolvedIncludes:after.includes.length,identifierRenames:0,identifierWeight:0.2};
if(process.argv.includes('--require-twenty'))assert.ok(result.bodyReductionPercent>=20,'Comment prose reduction is below 20%');
console.log(JSON.stringify(result,null,2));
