import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { repositoryRoot, generate } from '../../scripts/lib/contract-atlas-metadata.mjs';
import { pretty } from '../../scripts/lib/contract-atlas-pipeline.mjs';

export async function fixture() {
  const parent=path.join(os.tmpdir(),'pmltwin-contract-generation-tests'); await fs.mkdir(parent,{recursive:true});
  const directory=await fs.realpath(await fs.mkdtemp(path.join(parent,'case-'))),sourceRoot=path.join(directory,'source'); await fs.mkdir(sourceRoot);
  // Windows TEMP may use an 8.3 alias. Match the canonical paths used by bounded
  // repository reads, including the interleaved-source test's exact read hook.
  const root=await fs.realpath(sourceRoot);
  const paths=new Set(generate(repositoryRoot).internal.sources.map(s=>s.path));
  for(const folder of ['docs/presentation/contract-atlas','docs/design/contract-atlas/source','docs/design/contract-atlas/figma-plugin','scripts/lib']) {
    for(const entry of await fs.readdir(path.join(repositoryRoot,folder))) if(/\.(?:js|mjs|css|html|json)$/.test(entry)) paths.add(folder+'/'+entry);
  }
  for(const file of ['scripts/serve-contract-atlas.mjs','scripts/build-contract-atlas-design.mjs','scripts/build-contract-atlas-theme.mjs']) paths.add(file);
  for(const file of ['docs/contracts/method-bindings.json'])if(await fs.stat(path.join(repositoryRoot,file)).then(s=>s.isFile(),()=>false))paths.add(file);
  for(const relative of paths) { const target=path.join(root,relative); await fs.mkdir(path.dirname(target),{recursive:true}); await fs.copyFile(path.join(repositoryRoot,relative),target); }
  const source=path.join(root,'docs/contracts/presentation.json');
  return {directory,root,assetRoot:root,outputRoot:path.join(directory,'live'),source,read:async()=>JSON.parse(await fs.readFile(source,'utf8')),write:async data=>fs.writeFile(source,pretty(data))};
}
export const delay=ms=>new Promise(resolve=>setTimeout(resolve,ms));
export async function waitFor(coordinator,predicate,timeout=15000) {
  const deadline=Date.now()+timeout;
  while(Date.now()<deadline) { if(predicate(coordinator.state())) return coordinator.state(); await delay(25); }
  throw Error('Pipeline timeout: '+JSON.stringify(coordinator.state()));
}
