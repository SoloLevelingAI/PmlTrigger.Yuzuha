import test from 'node:test';
import assert from 'node:assert/strict';
import '../docs/presentation/contract-atlas/disclosure.js';
const Controller=globalThis.ContractAtlasDisclosure.Controller;
test('explicit global actions clear local overrides and govern unseen content and later scopes',()=>{
 const c=new Controller();assert.equal(c.isOpen('feature:a|card-details'),false);c.record('feature:a|card-details',true);assert.equal(c.getState().custom,true);
 c.setAll('full');assert.equal(c.getState().custom,false);assert.equal(c.isOpen('method:new|source'),true);c.setScope('import:example');assert.equal(c.isOpen('script:unseen|contract'),true);
 c.record('script:unseen|contract',false);c.setAll('summary');c.setScope('published');assert.equal(c.isOpen('feature:a|card-details'),false);assert.equal(c.getState().overrides,0);
});
test('local reading survives navigation scope reuse and remains isolated from independent drafts',()=>{
 const c=new Controller();c.record('feature:a|contract',true);c.setScope('draft:one');assert.equal(c.isOpen('feature:a|contract'),false);c.record('script:b|contract',true);c.setScope('published');assert.equal(c.isOpen('feature:a|contract'),true);assert.equal(c.isOpen('script:b|contract'),false);
 c.setScope('draft:one');assert.equal(c.isOpen('script:b|contract'),true);c.setScope('import:two');assert.equal(c.isOpen('script:b|contract'),false);
});
test('no-op native toggles do not generate state loops and removed owners are pruned',()=>{
 const changes=[],c=new Controller({onChange:change=>changes.push(change)});assert.equal(c.record('feature:a|contract',false),false);assert.equal(changes.length,0);assert.equal(c.record('feature:a|contract',true),true);assert.equal(c.record('feature:a|contract',true),false);c.record('feature:removed|sources',true);c.record('reference|reference:goals',true);
 c.prune(['feature:a']);assert.equal(c.getState().overrides,2);assert.equal(c.isOpen('feature:removed|sources'),false);assert.equal(c.isOpen('reference|reference:goals'),true);
 c.restore('full');assert.equal(c.getState().overrides,0);assert.equal(c.isOpen('feature:new|contract'),true);c.restore('invalid');assert.equal(c.mode,'summary');assert.equal(c.record('bad',true),false);
});
