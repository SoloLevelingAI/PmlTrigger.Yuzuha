import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {themeCss} from '../scripts/build-contract-atlas-theme.mjs';
const tokens=JSON.parse(fs.readFileSync(new URL('../docs/presentation/contract-atlas/tokens.json',import.meta.url)));
const roles=['overview','contract','flow','implementation','evidence','reference'];
const light={background:'#F4F6F5',surface:'#FFFFFF',soft:'#E7EFEB',text:'#18342F',muted:'#526C65',border:'#CBD9D3',accent:'#286951',warning:'#91601C',boundary:'#6D857B'};
const dark={background:'#10191C',surface:'#192428',soft:'#243439',text:'#EFF3E9',muted:'#A8B9B6',border:'#354747',accent:'#C5EC9B',warning:'#EFC17F',boundary:'#70897F'};
function luminance(hex){const c=[1,3,5].map(i=>parseInt(hex.slice(i,i+2),16)/255).map(v=>v<=0.04045?v/12.92:((v+.055)/1.055)**2.4);return c[0]*.2126+c[1]*.7152+c[2]*.0722;}
test('independent section tokens retain theme roles and generate all safe CSS variables with readable neutral text',()=>{
 assert.deepEqual(tokens.themes,{light,dark});assert.deepEqual(Object.keys(tokens.sections),roles);const css=themeCss(tokens);
 for(const theme of ['light','dark'])for(const role of roles){const section=tokens.sections[role][theme];for(const field of ['accent','surface'])assert.ok(css.includes(`--atlas-section-${role}-${field}:${section[field]}`));const a=luminance(tokens.themes[theme].text),b=luminance(section.surface);assert.ok((Math.max(a,b)+.05)/(Math.min(a,b)+.05)>=4.5,theme+'/'+role);}
 for(const [role,t]of Object.entries(tokens.typography)){assert.ok(css.includes(`--atlas-type-${role}-size:${t.size}px`));assert.ok(css.includes(`--atlas-type-${role}-weight:${t.weight}`));assert.ok(css.includes(`--atlas-type-${role}-line-height:${t.lineHeight}`));}
 assert.equal(tokens.typography.body.size,16);assert.ok(tokens.typography.card.size>=14);assert.ok(tokens.typography.meta.size>=12);
});
test('unsafe/missing section and typography candidates fail before CSS generation',()=>{
 for(const mutate of [t=>delete t.sections.reference,t=>t.sections.contract.dark.surface=t.themes.dark.text,t=>t.sections.flow.light.accent='red;display:none',t=>t.typography.body.size=10,t=>t.typography.meta.weight='400;display:none',t=>t.typography.card.lineHeight=0]){const bad=structuredClone(tokens);mutate(bad);assert.throws(()=>themeCss(bad));}
});
