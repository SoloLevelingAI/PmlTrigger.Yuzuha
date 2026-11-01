import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import assert from 'node:assert/strict';
import {argb,outputs,root} from '../scripts/build-ion-wpf-themes.mjs';

test('CSS alpha is converted to WPF ARGB without losing the six-digit color',()=>{
 assert.equal(argb('#FCFFF4E8'),'#E8FCFFF4');
 assert.equal(argb('#151810'),'#151810');
 for(const invalid of ['red','#123','#1122334455','"/>'])assert.throws(()=>argb(invalid));
});

test('six native overlays are generated from the shared source and are current',()=>{
 const generated=outputs();assert.equal(generated.size,7);
 for(const [file,expected]of generated)assert.equal(fs.readFileSync(path.join(root,file),'utf8'),expected,`${file} is stale`);
 const catalog=generated.get('src/YuzuhaToolkit.Desktop/DesktopPalettes.Generated.cs');
 for(const id of ['lime','cyan','violet','orange','rose','graphite'])assert.ok(catalog.includes(`"ion-${id}"`));
});

test('every overlay retains base semantic brush coverage and status colors',()=>{
 const palettes=JSON.parse(fs.readFileSync(path.join(root,'docs/presentation/contract-style-lab/ion/themes.json'),'utf8')).themes;
 const read=text=>new Map([...text.matchAll(/<SolidColorBrush x:Key="([^"]+)" Color="([^"]+)"/g)].map(([,key,color])=>[key,color]));
 const generated=outputs();
 for(const palette of palettes){
  const base=read(fs.readFileSync(path.join(root,`src/YuzuhaToolkit.Desktop/Themes/${palette.scheme==='dark'?'Dark':'Light'}.xaml`),'utf8'));
  const overlay=read(generated.get(`src/YuzuhaToolkit.Desktop/Themes/Ion-${palette.id}.xaml`));
  for(const [key,color]of base){assert.ok(overlay.has(key),`${palette.id}: missing ${key}`);if(/(?:Success|Warning|Danger|Amber)/.test(key))assert.equal(overlay.get(key),argb(color),`${palette.id}: status ${key}`);}
  assert.equal(overlay.get('Ion.Acid'),argb(palette.palette.acid));
  assert.equal(overlay.get('Ion.Glass'),argb(palette.palette.glass));
 }
});
