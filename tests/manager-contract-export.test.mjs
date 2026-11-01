import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import {artifacts,schemaType} from '../scripts/export-manager-contracts.mjs';
import {invokeCli} from '../scripts/lib/manager-cli-client.mjs';

test('export only promises read-only HTTP metadata and exact registered CLI inputs',()=>{
 const catalog={journey:{schema:'pmltwin.manager-journey/1',stages:[{operationIds:['evars.apply']}]},operations:[{id:'evars.apply',inputSchema:{type:'object',properties:{planId:{type:'string'},mode:{type:'string',enum:['copy','reference']}},required:['planId'],additionalProperties:false}}]};
 const output=artifacts(catalog),openapi=JSON.parse(output.get('manager-journey.openapi.json'));
 assert.deepEqual(Object.keys(openapi.paths),['/api/manager/contracts','/api/manager/operations','/api/manager/operations/{id}']);assert(Object.values(openapi.paths).every(route=>Object.keys(route).join()==='get'));
 assert.equal(openapi.paths['/api/manager/contracts'].get.responses['200'].content['application/json'].schema.$ref,'#/components/schemas/ManagerContractExport');assert.equal(openapi.components.schemas.OperationResult.properties.data,true);assert.deepEqual(openapi['x-cli-input-schemas']['evars.apply'],catalog.operations[0].inputSchema);
 assert.match(output.get('manager-journey-client.ts'),/"evars.apply": \{ "planId": string; "mode"\?: "copy" \| "reference" \}/);assert.match(output.get('manager-journey-client.ts'),/data\?: unknown/);assert.match(output.get('manager-journey-client.mjs'),/rawJson/);
 catalog.journey.stages[0].operationIds.push('not.registered');assert.throws(()=>artifacts(catalog),/Unregistered/);
 assert.equal(schemaType({anyOf:[{type:'null'},{type:'integer'}]}),'null | number');
 assert.equal(schemaType({type:'object',additionalProperties:{type:'string'}}),'Record<string, string>');
 assert.equal(schemaType({type:'object',properties:{label:{type:'string'}},required:['label'],additionalProperties:{type:'string'}}),'{ "label": string } & Record<string, string>');
 assert.equal(schemaType({type:'object',additionalProperties:{type:'array',items:{type:'boolean'}}}),'Record<string, (boolean)[]>');
});

test('CLI adapter uses stdin, preserves Int64 ticks, passes only explicit grants and never retries',async t=>{
 const directory=await fs.mkdtemp(path.join(os.tmpdir(),'manager-ts-cli-'));t.after(()=>fs.rm(directory,{recursive:true,force:true}));
 const fixture=path.join(directory,'fixture.mjs');await fs.writeFile(fixture,"let input='';process.stdin.setEncoding('utf8');for await(const b of process.stdin)input+=b;process.stdout.write(JSON.stringify({args:process.argv.slice(2),input:JSON.parse(input)}));");
 const ticks='638975584001234567',password='fixture-secret-not-argv';const reply=await invokeCli('launch.execute',{password},{executable:process.execPath,prefixArgs:[fixture],context:{processId:123,expectedModel:'DESIGN',expectedProcessStartTimeUtcTicks:ticks,allowLaunch:true}});
 const value=JSON.parse(reply.rawJson);assert(!value.args.join(' ').includes(password));assert.equal(value.input.password,password);assert(value.args.includes('--input-stdin'));assert(value.args.includes('--allow-launch'));assert(!value.args.includes('--allow-write'));assert(!value.args.includes('--allow-execution'));assert.equal(value.args[value.args.indexOf('--expected-start-time-utc-ticks')+1],ticks);
 assert.throws(()=>invokeCli('connection.test',{}, {executable:process.execPath,context:{processId:123,expectedProcessStartTimeUtcTicks:Number(ticks)}}),/decimal string/);
 assert.throws(()=>invokeCli('evars.apply',{}, {executable:process.execPath,context:{allowWrite:'true'}}),/explicit booleans/);
 const fail=path.join(directory,'fail.mjs'),count=path.join(directory,'attempts');await fs.writeFile(fail,"import fs from 'node:fs';fs.appendFileSync(process.argv[2],'x');process.stdout.write('{\"error\":\"unknown\"}');process.exit(7);");
 const failed=await invokeCli('evars.apply',{}, {executable:process.execPath,prefixArgs:[fail,count],context:{allowWrite:true}});assert.equal(failed.exitCode,7);assert(failed.mayHaveExecuted);assert.equal(await fs.readFile(count,'utf8'),'x');
 const slow=path.join(directory,'slow.mjs');await fs.writeFile(slow,'setTimeout(()=>{},10000);');await assert.rejects(invokeCli('launch.execute',{}, {executable:process.execPath,prefixArgs:[slow],context:{allowLaunch:true},timeoutMs:30}),error=>error.mayHaveExecuted&&/never retry/.test(error.message));
 await assert.rejects(invokeCli('evars.preview',{}, {executable:process.execPath,prefixArgs:[slow],timeoutMs:30}),error=>error.mayHaveExecuted,'a preview can already have persisted its local frozen plan');
});
