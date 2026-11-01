import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
export const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
export function argb(value){if(!/^#[0-9a-f]{6}(?:[0-9a-f]{2})?$/i.test(value))throw Error('Invalid ION color');return value.length===9?'#'+value.slice(7)+value.slice(1,7):value;}
export function outputs(base=root){
 const source=fs.readFileSync(path.join(base,'docs/presentation/contract-style-lab/ion/themes.json'),'utf8'),data=JSON.parse(source),digest=crypto.createHash('sha256').update(source).digest('hex');
 if(data.schema!=='ion-palettes/1'||data.themes.length!==6||new Set(data.themes.map(t=>t.id)).size!==6)throw Error('Expected six explicit ION palettes');
 const result=new Map();
 for(const theme of data.themes){
  if(!/^[a-z]+$/.test(theme.id)||!['light','dark'].includes(theme.scheme))throw Error('Invalid palette identity');
  const p=theme.palette,baseTheme=fs.readFileSync(path.join(base,'src/YuzuhaToolkit.Desktop/Themes/'+(theme.scheme==='dark'?'Dark':'Light')+'.xaml'),'utf8');
  const brushes=[...baseTheme.matchAll(/<SolidColorBrush x:Key="([^"]+)" Color="([^"]+)"\s*\/>/g)].map(([,key,color])=>{
   let next=color;
   if(/(?:Success|Warning|Danger|Amber)/.test(key))return [key,next];
   if(/(?:OnAccent|Icon.OnAccent|LogoLine)/.test(key))next=p.acidInk;
   else if(/(?:NavSelectedText)/.test(key))next=p.acidInk;
   else if(/(?:NavSelected|NavIndicator|Accent.Brand|Accent.Default|^Accent$|LogoDot|Surface.Logo)/.test(key))next=p.acid;
   else if(/(?:Text.Brand|Text.Sky|Text.Purple|NavText)/.test(key))next=p.link;
   else if(/(?:Text.Primary|Text.OnDark$)/.test(key))next=p.ink;
   else if(/(?:Text.Secondary|Text.Tertiary|Text.OnDarkMuted|Text.OnDarkFaint|NavMuted|Accent.Neutral)/.test(key))next=p.muted;
   else if(/(?:Border|\.Line)/.test(key))next=p.line;
   else if(/(?:Page|Sidebar$)/.test(key))next=p.paper;
   else if(/(?:Surface.Code)/.test(key))next=theme.scheme==='dark'?p.paper:'#151810';
   else if(/(?:OnCode|Text.Code)/.test(key))next=theme.scheme==='dark'?p.ink:'#F1F2E9';
   else if(/(?:Subtle|Soft|Selected|Tint|Track|ToggleOff|Account|HeroTag|SidebarPanel)/.test(key))next=p.soft;
   else if(/(?:Surface|Hero)/.test(key))next=p.surface;
   else if(/(?:Chart|Accent.Sky|Accent.Purple)/.test(key))next=p.chrome;
   return [key,next];
  });
  const native=[['Ion.Acid',p.acid],['Ion.AcidInk',p.acidInk],['Ion.Paper',p.paper],['Ion.Ink',p.ink],['Ion.Pink',p.pink],['Ion.Glass',p.glass],['Ion.Metal',p.metalMid]];
  const text='<?xml version="1.0" encoding="utf-8"?>\n<!-- Generated from ion/themes.json SHA256 '+digest+'; do not edit. -->\n<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">\n'+[...brushes,...native].map(([key,color])=>'  <SolidColorBrush x:Key="'+key+'" Color="'+argb(color)+'" />').join('\n')+'\n</ResourceDictionary>\n';
  result.set('src/YuzuhaToolkit.Desktop/Themes/Ion-'+theme.id+'.xaml',text);
 }
 result.set('src/YuzuhaToolkit.Desktop/DesktopPalettes.Generated.cs','// Generated from ion/themes.json SHA256 '+digest+'\nnamespace YuzuhaToolkit.Desktop;\n\n/// <summary>来自同一ION设计源的原生主题身份。</summary>\n/// <param name="Id">持久化主题标识。</param>\n/// <param name="Name">显示名称。</param>\n/// <param name="Scheme">原生窗口使用的明暗模式。</param>\ninternal sealed record DesktopPalette(string Id, string Name, string Scheme);\n\n/// <summary>Web、FigCraft与WPF共同设计源的生成目录。</summary>\ninternal static class DesktopPalettes\n{\n    /// <summary>六套ION主题，旧Light/Dark身份由主题服务保持兼容。</summary>\n    internal static readonly DesktopPalette[] All =\n    [\n'+data.themes.map(t=>'        new("ion-'+t.id+'", "'+t.name+'", "'+(t.scheme==='dark'?'Dark':'Light')+'"),').join('\n')+'\n    ];\n}\n');
 return result;
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 const check=process.argv.includes('--check');for(const [file,content]of outputs()){const target=path.join(root,file);if(check){if(!fs.existsSync(target)||fs.readFileSync(target,'utf8')!==content)throw Error('ION WPF theme drift: '+file);}else fs.writeFileSync(target,content);}
 console.log(JSON.stringify({palettes:6,mode:check?'check':'build'}));
}
