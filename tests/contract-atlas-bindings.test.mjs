import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {generate,repositoryRoot} from '../scripts/lib/contract-atlas-metadata.mjs';
const metadata=generate().publicData;
const config=JSON.parse(fs.readFileSync(path.join(repositoryRoot,'docs/contracts/method-bindings.json'),'utf8'));

test('every Feature and Script has one explicit binding or an honest scoped absence',()=>{
 const expected=[...metadata.features,...metadata.scripts].map(e=>e.id).sort();
 assert.equal(config.bindings.length,32);assert.deepEqual(config.bindings.map(b=>b.entityId).sort(),expected);
 assert.equal(new Set(config.bindings.map(b=>b.entityId)).size,32);
 for(const b of config.bindings){assert.ok(Array.isArray(b.symbolIds));if(!b.symbolIds.length){assert.ok(['unavailable','not-applicable'].includes(b.resolution));assert.ok(b.reason?.length>20);}else assert.ok(!b.resolution||b.resolution==='resolved');}
 for(const id of ['feature:caf','feature:plugins','feature:database','script:ui-pending-gate'])assert.equal(config.bindings.find(b=>b.entityId===id).resolution,'unavailable');
 for(const id of ['script:pml-development','script:host-bootstrap','script:sdk-acceptance','script:runtime-verification'])assert.equal(config.bindings.find(b=>b.entityId===id).resolution,'not-applicable');
 assert.match(config.bindings.find(b=>b.entityId==='script:ribbon-frozen-deployment').reason,/backend.*not compiled/i);
});

const graphPath=path.join(repositoryRoot,config.analysis.graph);
test('reviewed bindings resolve exact captured symbols and remain inside the direct method catalog budgets',{skip:!fs.existsSync(graphPath)},()=>{
 const graph=JSON.parse(fs.readFileSync(graphPath,'utf8'));const nodes=new Set(graph.nodes.map(n=>n.id));const roots=new Set(config.bindings.flatMap(b=>b.symbolIds));
 for(const id of roots)assert.ok(nodes.has(id),'Missing exact symbol '+id);
 const selected=new Set(roots);for(const e of graph.edges)if(roots.has(e.from)||roots.has(e.to)){selected.add(e.from);selected.add(e.to);}
 const methods=new Set([...selected].filter(id=>id.split('::')[1]?.startsWith('M:')||id.includes('::lambda:')));
 const edges=graph.edges.filter(e=>methods.has(e.from)&&methods.has(e.to));assert.ok(methods.size<=500,`${methods.size} methods`);assert.ok(edges.length<=1500,`${edges.length} edges`);
 for(const id of ['script:frozen-copy','script:migration-preview','script:knowledge-import'])assert.ok(config.bindings.find(b=>b.entityId===id).symbolIds.length);
});
