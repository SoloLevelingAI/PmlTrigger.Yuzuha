import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import {filterItems,csvRows} from '../src/YuzuhaToolkit.WebApps/wwwroot/table.js';
test('web app table filters all rows and preserves large values',()=>{
 const rows=Array.from({length:2410},(_,i)=>({Path:`ELDATA.|${i+1}|`,Type:'STRING',Value:`value ${i+1}`}));
 assert.equal(filterItems(rows,'ELDATA.|2410|').length,1);assert.equal(rows.length,2410);
});
test('CSV quotes data and protects leading whitespace formulas',()=>{
 const csv=csvRows([{Path:'p',Type:'STRING',Value:' =SUM(A1)'},{Path:'q',Type:'STRING',Value:'a,"b"\nc'}]);
 assert.match(csv,/"' =SUM\(A1\)"/);assert.match(csv,/"a,""b""\nc"/);
});
test('UI has no inline execution, remote assets or unsafe HTML insertion',()=>{
 const dir=new URL('../src/YuzuhaToolkit.WebApps/wwwroot/',import.meta.url);
 const html=fs.readFileSync(new URL('index.html',dir),'utf8'),js=fs.readFileSync(new URL('app.js',dir),'utf8');
 assert.doesNotMatch(html,/https?:\/\/|onclick=/);assert.doesNotMatch(js,/innerHTML|eval\(|new Function/);
 // Compile the actual client after replacing only its static module declaration.
 new vm.Script('(async()=>{'+js.replace(/^import .*;\n/,'')+'})()');
 assert.match(js,/requestId,catalogRevision:catalog.revision/);assert.match(js,/sessionStorage.setItem/);
});
