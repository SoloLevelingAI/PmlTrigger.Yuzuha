import test from 'node:test';
import assert from 'node:assert/strict';
import {Script} from 'node:vm';
import {readTableSnapshot,renderPmlDataTable,filterItems,csvRows} from '../scripts/pml-data-table.mjs';

test('IPC rows retain their path, type, empty and Unicode values',()=>{
  const items=[{Path:'ELDATA.|1|',Type:'STRING',Value:'中文, "value"\nnext'},{Path:'empty',Type:'STRING',Value:''}];
  assert.deepEqual(readTableSnapshot({success:true,data:{Items:items,UnparsedCount:1}}).items,items);
  assert.equal(readTableSnapshot({data:{Items:items,UnparsedCount:1}}).unparsed,1);
});
test('workflow reports expose exact expected values and failed state without declaring a pass',()=>{
  const report={status:'FailedOrUnknown',manifest:{expected:{Revision:'v2'}},steps:[{name:'read-results',status:'Passed',result:{Items:[{Path:'Revision',Type:'STRING',Value:'v1'}]}}]};
  const result=readTableSnapshot(report);assert.equal(result.status,'FailedOrUnknown');assert.equal(result.expected.Revision,'v2');
  assert.throws(()=>readTableSnapshot({steps:[]}));
});
test('embedded data cannot break out of the JSON script; cells use textContent',()=>{
  const html=renderPmlDataTable({Items:[{Path:'x',Type:'STRING',Value:'</script><script>bad()</script>'}]});
  assert.ok(!html.includes('<script>bad()'));
  assert.match(html,/\\u003c\/script>/);
  assert.match(html,/cell.textContent/);
  assert.ok(!html.includes('innerHTML='));
  const script=html.split('<script>')[1].split('</script>')[0];
  assert.doesNotThrow(()=>new Script(script));
});
test('the same filtering and CSV functions embedded in the page handle quotes, newlines and formula-like text',()=>{
  const items=[{Path:'ELDATA.|1|',Type:'STRING',Value:'中文, "value"\nnext'},{Path:'ELDATA.|2|',Type:'STRING',Value:'=1+1'}];
  assert.deepEqual(filterItems(items,'中文'),[items[0]]);
  assert.equal(filterItems(items,'string').length,2);
  assert.equal(filterItems(items,'absent').length,0);
  assert.equal(csvRows(items),'"Path","Type","Value"\r\n"ELDATA.|1|","STRING","中文, ""value""\nnext"\r\n"ELDATA.|2|","STRING","\'=1+1"');
});
