import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdir, mkdtemp, writeFile, readFile, rm, readdir } from 'node:fs/promises';
import { resolve, join, dirname } from 'node:path';
import { create } from 'xmlbuilder2';
import { exampleProject } from '../packages/uic-core/example.ts';
import { exportUic } from '../packages/uic-core/index.ts';
import { importUic } from '../packages/uic-core/import.ts';
import { parseUic, child, value } from '../packages/uic-core/roundtrip.ts';
import { UicWorkspace } from '../packages/uic-workspace/service.ts';
import { WorkspaceStore } from '../packages/uic-workspace/store.ts';
import { deployUic, previewDeployment } from '../packages/uic-deployment/index.ts';

async function fixture() {
  const parent = resolve('.runtime'); await mkdir(parent, { recursive: true });
  const root = await mkdtemp(join(parent, 'history-test-')), install = join(root, 'install');
  await mkdir(install);
  const monPath = join(install, 'mon.exe'), customPath = join(root, 'YtoolsAM.uic'), manifestPath = join(install, 'DesignCustomization.xml');
  await writeFile(monPath, 'Non-executable test fixture');
  const p = await exampleProject();
  const xml = (await exportUic(p)).replace('<Caption>查询工具</Caption>', '<ShortCut>Ctrl1</ShortCut><Caption>查询工具</Caption>').replace('<Group Name=', '<Group DialogBoxLauncherKey="preserved" Name=').replace('<Tool Name=', '<Tool ImageOnly="False" Name=');
  await writeFile(customPath, xml);
  await writeFile(join(install, 'design.uic'), xml);
  const escaped = customPath.replaceAll('&', '&amp;');
  const manifest = '<UICustomizationSet>\n<!-- keep user comment -->\n<UICustomizationFiles>\n<CustomizationFile Name="Module" Path="design.uic"/>\n<CustomizationFile Name="Ytools" Path="' + escaped + '"/>\n</UICustomizationFiles>\n</UICustomizationSet>';
  await writeFile(manifestPath, manifest);
  return { root, install, monPath, customPath, manifestPath, manifest, xml, workspace: new UicWorkspace(new WorkspaceStore(join(root, 'state'))) };
}
async function clean(root: string) {
  assert.equal(dirname(root), resolve('.runtime'));
  await rm(root, { recursive: true, force: true });
}

test('only five automatic drafts remain; saving an identical draft creates a lasting snapshot with monotonic numbering', async () => {
  const f = await fixture();
  try {
    const imported = await f.workspace.import({ path: f.customPath });
    const project = imported.project;
    for (let i = 0; i < 8; i++) { project.buttons[0].macro = 'draft-' + i; await f.workspace.save(project); }
    let versions = await f.workspace.store.versions(project.documentId!);
    assert.equal(versions.filter(v => v.kind === 'edit').length, 5);
    assert.equal(versions.length, 6);
    const draft = versions[0];
    const checkpoint = await f.workspace.save(project, 'save', '用户手动保存');
    assert.equal(checkpoint.version.kind, 'save');
    assert.equal(checkpoint.version.sequence, draft.sequence + 1);
    for (let i = 0; i < 9; i++) { project.buttons[0].macro = 'later-' + i; await f.workspace.save(project); }
    const restarted = new WorkspaceStore(join(f.root, 'state'));
    versions = await restarted.versions(project.documentId!);
    assert.equal(versions.filter(v => v.kind === 'edit').length, 5);
    assert(versions.some(v => v.id === imported.version.id));
    assert(versions.some(v => v.id === checkpoint.version.id));
    await assert.rejects(restarted.version(project.documentId!, draft.id), /版本不存在/);
    const meta = await restarted.get(project.documentId!);
    assert.equal(meta.count, versions.length);
    assert.equal(meta.latestSequence, versions[0].sequence);
    assert.equal((await readdir(join(f.root, 'state', 'documents', project.documentId!, 'versions'))).filter(n => n.endsWith('.json')).length, versions.length);
    const restored = await f.workspace.restore(project.documentId!, checkpoint.version.id);
    assert.equal(restored.project.buttons[0].macro, 'draft-7');
  } finally { await clean(f.root); }
});

test('shared UIC updates once without choosing a module; adding a module only changes its manifest', async () => {
  const f = await fixture();
  try {
    const draftPath = join(f.install, 'DraftCustomization.xml');
    await writeFile(draftPath, f.manifest.replace('Name="Ytools"', 'Name="YtoolsDraft"'));
    const imported = await f.workspace.import({ path: f.customPath });
    const project = imported.project;
    project.preferredBinding = { monPath: f.monPath, manifestName: 'DesignCustomization.xml', entryName: 'Ytools' };
    project.buttons[0].macro = 'updated shared file';
    const preview = await f.workspace.previewFile(project);
    assert.equal(preview.references.length, 2);
    const beforeDraft = await readFile(draftPath), beforeDesign = await readFile(f.manifestPath);
    const result = await f.workspace.updateFile({ project, expectedUicHash: preview.uicHash });
    assert.deepEqual(await readFile(f.manifestPath), beforeDesign);
    assert.deepEqual(await readFile(draftPath), beforeDraft);
    assert.equal(await readFile(join(result.backupDirectory!, 'YtoolsAM.uic'), 'utf8'), f.xml);
    assert.match(await readFile(f.customPath, 'utf8'), /updated shared file/);
    assert.equal((await f.workspace.store.get(project.documentId!)).bindings.length, 2);
    await assert.rejects(f.workspace.updateFile({ project, expectedUicHash: preview.uicHash }), /已变化/);
    const extraPath = join(f.install, 'ParagonCustomization.xml');
    await writeFile(extraPath, '<UICustomizationSet><UICustomizationFiles/></UICustomizationSet>');
    const choices = await f.workspace.modules({ project, monPath: f.monPath });
    assert.deepEqual(choices.modules.find(m => m.manifestName === 'DesignCustomization.xml')!.entryNames, ['Ytools']);
    assert.deepEqual(choices.modules.find(m => m.manifestName === 'DraftCustomization.xml')!.entryNames, ['YtoolsDraft']);
    assert.deepEqual(choices.modules.find(m => m.manifestName === 'ParagonCustomization.xml')!.entryNames, []);
    assert.deepEqual(await readFile(f.manifestPath), beforeDesign, 'collecting transfer choices is read-only');
    const context = { project, monPath: f.monPath, manifestName: 'ParagonCustomization.xml', entryName: 'Ytools' };
    const add = await f.workspace.registration(context);
    assert.equal(add.beforeXml, '<UICustomizationSet><UICustomizationFiles/></UICustomizationSet>');
    assert.deepEqual(add.beforeEntries, []);
    assert.deepEqual(add.addedEntries, [{ name: 'Ytools', path: f.customPath }]);
    assert.deepEqual(add.afterEntries, add.addedEntries);
    assert.equal(add.uicModified, false);
    assert.equal(await readFile(extraPath, 'utf8'), add.beforeXml, 'preview must show the actual unchanged source XML');
    const currentUic = await readFile(f.customPath);
    await f.workspace.registration({ ...context, expectedManifestHash: add.manifestHash, expectedUicHash: add.uicHash }, true);
    assert.equal(await readFile(extraPath, 'utf8'), add.afterXml, 'actual write must equal the XML shown in preview');
    assert.deepEqual(await readFile(f.customPath), currentUic);
    assert.deepEqual(await readFile(f.manifestPath), beforeDesign);
    assert.deepEqual(await readFile(draftPath), beforeDraft);
    assert.match(await readFile(extraPath, 'utf8'), /Name="Ytools"/);
    assert.equal((await f.workspace.previewFile(project)).references.length, 3);
    await assert.rejects(f.workspace.registration({ ...context, entryName: 'AnotherName' }), /已经引用/);
    const native = (await f.workspace.import({ path: join(f.install, 'design.uic') })).project;
    native.preferredBinding = project.preferredBinding;
    await assert.rejects(f.workspace.previewFile(native), /只读/);
    const orphanPath = join(f.root, 'Unregistered.uic'); await writeFile(orphanPath, f.xml);
    const orphan = (await f.workspace.import({ path: orphanPath })).project;
    orphan.preferredBinding = project.preferredBinding;
    assert.equal((await f.workspace.previewFile(orphan)).references.length, 0, 'an existing file needs no XML registration to be updated');
  } finally { await clean(f.root); }
});

test('module collection distinguishes relative existing references, available modules and unreadable manifests', async () => {
  const f = await fixture();
  try {
    const project = (await f.workspace.import({ path: f.customPath })).project;
    await writeFile(join(f.install, 'RelativeCustomization.xml'), '<UICustomizationSet><UICustomizationFiles><CustomizationFile Name="SharedRelative" Path="../YtoolsAM.uic"/></UICustomizationFiles></UICustomizationSet>');
    await writeFile(join(f.install, 'EmptyCustomization.xml'), '<UICustomizationSet><UICustomizationFiles/></UICustomizationSet>');
    await writeFile(join(f.install, 'BrokenCustomization.xml'), '<Unsupported/>');
    const collection = await f.workspace.modules({ project, monPath: f.monPath });
    assert.equal(collection.modules.length, 4);
    assert.deepEqual(collection.modules.find(row => row.manifestName === 'RelativeCustomization.xml')!.entryNames, ['SharedRelative']);
    assert.equal(collection.modules.find(row => row.manifestName === 'EmptyCustomization.xml')!.error, '');
    assert(collection.modules.find(row => row.manifestName === 'BrokenCustomization.xml')!.error);
    assert.equal(await readFile(f.customPath, 'utf8'), f.xml);
  } finally { await clean(f.root); }
});
test('imported UIC retains shortcuts, extra attributes, names and opaque nodes while editing fields and moving tools', async () => {
  const f = await fixture();
  try {
    const p = await importUic(f.xml, 'YtoolsAM.uic', f.customPath);
    assert.equal(p.buttons.length, 4); assert.equal(p.buttons[0].sourceId, 'Custom.Button.1');
    p.buttons[0].macro = "show !!Changed('中文 &lt; & <x>')";
    p.buttons[0].tooltipText = '修改 <提示> & 文字';
    p.buttons[0].id = 'Renamed.Button';
    const ref = p.tabs[0].groups[0].items.shift()!; ref.buttonId = p.buttons[0].id; p.tabs[0].groups[1].items.push(ref);
    p.tabs[0].groups[0].caption = '修改后的分组';
    const xml = await exportUic(p), root = parseUic(xml).documentElement;
    const tool = Array.from(child(root, 'Tools').childNodes).find((n: any) => n.nodeType === 1 && n.getAttribute('Name') === 'Renamed.Button') as any;
    assert.equal(value(tool, 'ShortCut'), 'Ctrl1');
    assert.equal(value(child(tool, 'Command'), 'Macro'), p.buttons[0].macro);
    assert.equal(value(tool, 'Tooltip'), p.buttons[0].tooltipText);
    assert.equal(root.getElementsByTagNameNS('www.aveva.com', 'Group')[0].getAttribute('DialogBoxLauncherKey'), 'preserved');
    const moved = Array.from(root.getElementsByTagNameNS('www.aveva.com', 'Tool')).find((n: any) => n.getAttribute('Name') === 'Renamed.Button') as any;
    assert.equal(moved.getAttribute('ImageOnly'), 'False');
    assert.equal(await readFile(f.customPath, 'utf8'), f.xml, 'import/export never writes source');
  } finally { await clean(f.root); }
});
test('unsupported visible tools and DTD cannot be silently imported or discarded', async () => {
  const f = await fixture();
  try {
    await assert.rejects(importUic(f.xml.replace('<Type>Macro</Type>', '<Type>Instance</Type>'), 'advanced.uic'));
    await assert.rejects(importUic('<!DOCTYPE x SYSTEM "file:///secret">' + f.xml, 'invalid.uic'));
  } finally { await clean(f.root); }
});
test('versions persist across store recreation, save drafts, restore as a new version and serialize concurrent saves', async () => {
  const f = await fixture();
  try {
    const imported = await f.workspace.import({ path: f.customPath });
    const baseline = imported.version.id, project = imported.project;
    project.buttons[0].macro = 'show !!Updated';
    const saved = await f.workspace.save(project);
    const draft = structuredClone(project); draft.buttons[0].macro = '';
    assert.equal((await f.workspace.save(draft)).version.valid, false);
    const restarted = new UicWorkspace(new WorkspaceStore(join(f.root, 'state')));
    assert.equal((await restarted.store.list()).length, 1);
    assert.equal((await restarted.store.version(project.documentId!, saved.version.id)).project.buttons[0].macro, 'show !!Updated');
    const restored = await restarted.restore(project.documentId!, baseline);
    assert.equal(restored.project.buttons[0].macro, imported.version.project.buttons[0].macro);
    assert.equal(restored.version.kind, 'restore');
    await Promise.all([restarted.save(restored.project, 'save', 'A'), restarted.save({ ...restored.project, namespace: 'ChangedNamespace' }, 'save', 'B')]);
    const versions = await restarted.store.versions(project.documentId!);
    assert.equal(new Set(versions.map(v => v.sequence)).size, versions.length);
    assert.equal(await readFile(f.customPath, 'utf8'), f.xml);
  } finally { await clean(f.root); }
});
test('management protects native entries, adopts existing absolute path and deploys in place without duplicate hash UIC', async () => {
  const f = await fixture();
  try {
    const context = { monPath: f.monPath, manifestName: 'DesignCustomization.xml' };
    const list = await f.workspace.inventory(context.monPath, context.manifestName);
    const native = list.rows.find(r => r.name === 'Module')!, custom = list.rows.find(r => r.name === 'Ytools')!;
    assert.equal(native.origin, 'protected'); assert.equal(custom.origin, 'custom');
    await assert.rejects(f.workspace.adopt({ ...context, key: native.key, expectedManifestHash: list.manifestHash }), (e: any) => e.status === 403);
    await assert.rejects(f.workspace.toggle({ ...context, key: native.key, expectedManifestHash: list.manifestHash, enabled: false }), (e: any) => e.status === 403);
    const adopted = await f.workspace.adopt({ ...context, key: custom.key, expectedManifestHash: list.manifestHash });
    assert.equal(await readFile(f.manifestPath, 'utf8'), f.manifest);
    adopted.project.buttons[0].macro = 'show !!EditedExisting';
    const plan = await f.workspace.preview({ ...context, project: adopted.project });
    assert.equal(plan.uicPath, f.customPath); assert.equal(plan.entryName, 'Ytools'); assert.equal(plan.managed, true);
    await f.workspace.deploy({ ...context, project: adopted.project, expectedManifestHash: plan.manifestHash, expectedUicHash: plan.uicHash });
    assert((await readFile(f.customPath, 'utf8')).includes('EditedExisting'));
    const entries = (create(await readFile(f.manifestPath, 'utf8')).node as any).documentElement.getElementsByTagName('CustomizationFile');
    assert.equal(entries.length, 2); assert.equal(entries[1].getAttribute('Path'), f.customPath);
    assert(!(await readdir(f.install)).some(n => /^RibbonStudio.*\.uic$/.test(n)));
    assert((await f.workspace.store.versions(adopted.document.id)).some(v => v.kind === 'deploy' && v.artifacts?.uicAfterBase64));
  } finally { await clean(f.root); }
});
test('managed loading can be removed and restored without deleting UIC or losing snapshots, stale edits are refused', async () => {
  const f = await fixture();
  try {
    const context = { monPath: f.monPath, manifestName: 'DesignCustomization.xml' };
    let list = await f.workspace.inventory(context.monPath, context.manifestName);
    const row = list.rows.find(r => r.name === 'Ytools')!;
    const owned = await f.workspace.adopt({ ...context, key: row.key, expectedManifestHash: list.manifestHash });
    list = await f.workspace.inventory(context.monPath, context.manifestName);
    await f.workspace.toggle({ ...context, key: row.key, expectedManifestHash: list.manifestHash, enabled: false });
    assert.equal(await readFile(f.customPath, 'utf8'), f.xml);
    assert(!(await readFile(f.manifestPath, 'utf8')).includes('Name="Ytools"'));
    const disabled = await f.workspace.inventory(context.monPath, context.manifestName);
    assert.equal(disabled.rows.find(r => r.name === 'Ytools')!.enabled, false);
    await assert.rejects(f.workspace.toggle({ ...context, key: row.key, expectedManifestHash: list.manifestHash, enabled: true }), (e: any) => e.status === 409);
    await f.workspace.toggle({ ...context, key: row.key, expectedManifestHash: disabled.manifestHash, enabled: true });
    assert((await readFile(f.manifestPath, 'utf8')).includes('keep user comment'));
    assert((await f.workspace.store.versions(owned.document.id)).some(v => v.kind === 'disable' && v.artifacts?.manifestAfterBase64));
  } finally { await clean(f.root); }
});

test('adopting after editing an imported file preserves the edited draft and original path', async () => {
  const f = await fixture();
  try {
    const imported = await f.workspace.import({ bytesBase64: Buffer.from(f.xml).toString('base64'), fileName: 'YtoolsAM.uic' });
    imported.project.buttons[0].macro = 'show !!EditBeforeAdoption';
    await f.workspace.save(imported.project);
    const context = { monPath: f.monPath, manifestName: 'DesignCustomization.xml' };
    const inventory = await f.workspace.inventory(context.monPath, context.manifestName);
    const row = inventory.rows.find(r => r.name === 'Ytools')!;
    assert.equal((await f.workspace.preview({ ...context, project: imported.project })).uicPath, f.customPath);
    const adopted = await f.workspace.adopt({ ...context, key: row.key, expectedManifestHash: inventory.manifestHash, documentId: imported.document.id });
    assert.equal(adopted.document.id, imported.document.id);
    assert.equal(adopted.project.buttons[0].macro, 'show !!EditBeforeAdoption');
    assert.equal((await f.workspace.preview({ ...context, project: adopted.project })).uicPath, f.customPath);
    assert.equal(await readFile(f.customPath, 'utf8'), f.xml);
  } finally { await clean(f.root); }
});

test('editing Macro preserves untouched rich tooltip formatting and invalid structural drafts are refused', async () => {
  const f = await fixture();
  try {
    const rich = f.xml.replace('&lt;p&gt;查询工具', '&lt;p&gt;&lt;b&gt;查询工具&lt;/b&gt;');
    const imported = await importUic(rich, 'rich.uic');
    const before = value(child(parseUic(rich).documentElement, 'Tools').firstElementChild, 'TooltipFormattedText');
    imported.buttons[0].macro = 'show !!DifferentMacro';
    const after = value(child(parseUic(await exportUic(imported)).documentElement, 'Tools').firstElementChild, 'TooltipFormattedText');
    assert.equal(after, before);
    await assert.rejects(f.workspace.save({ ...imported, tabs: [null] } as any), (e: any) => e.status === 422);
  } finally { await clean(f.root); }
});

test('old deployments require matching backup receipts and current UIC hash before becoming managed', async () => {
  const f = await fixture();
  try {
    const context = { monPath: f.monPath, manifestName: 'DesignCustomization.xml', project: await exampleProject() };
    const plan = await previewDeployment(context);
    await deployUic({ ...context, expectedManifestHash: plan.manifestHash, expectedUicHash: plan.uicHash });
    const inventory = await f.workspace.inventory(context.monPath, context.manifestName);
    const old = inventory.rows.find(r => r.name === plan.entryName)!;
    assert.equal(old.origin, 'managed');
    assert((await f.workspace.store.versions(old.documentId!)).some(v => v.kind === 'legacy-adopt'));
    const forgedName = 'RibbonStudio.1111111111111111';
    const current = await readFile(f.manifestPath, 'utf8');
    await writeFile(f.manifestPath, current.replace('</UICustomizationFiles>', '<CustomizationFile Name="' + forgedName + '" Path="RibbonStudio.1111111111111111.uic"/></UICustomizationFiles>'));
    await writeFile(join(f.install, forgedName + '.uic'), f.xml);
    assert.equal((await f.workspace.inventory(context.monPath, context.manifestName)).rows.find(r => r.name === forgedName)!.origin, 'unknown');
  } finally { await clean(f.root); }
});

test('import-edit-deploy directly overwrites original custom UIC with exact backup and preserves its loading path', async () => {
  const f = await fixture();
  try {
    const imported = await f.workspace.import({ path: f.customPath });
    const inventory = await f.workspace.inventory(f.monPath, 'DesignCustomization.xml');
    const opened = await f.workspace.openEntry({ monPath: f.monPath, manifestName: 'DesignCustomization.xml', key: inventory.rows.find(r => r.name === 'Ytools')!.key, documentId: imported.document.id });
    assert.equal(opened.documentId, imported.document.id);
    assert.equal(opened.preferredBinding?.entryName, 'Ytools');
    opened.buttons[0].macro = 'show !!DirectOverwrite';
    const context = { monPath: f.monPath, manifestName: 'DesignCustomization.xml', project: opened };
    const plan = await f.workspace.preview(context);
    assert.equal(plan.operation, 'overwrite-original');
    assert.equal(plan.uicPath, f.customPath);
    assert.equal((await f.workspace.store.get(imported.document.id)).bindings.length, 0, 'preview does not take ownership');
    assert.equal(await readFile(f.customPath, 'utf8'), f.xml);
    const deployed = await f.workspace.deploy({ ...context, expectedManifestHash: plan.manifestHash, expectedUicHash: plan.uicHash });
    assert.equal(await readFile(join(deployed.backupDirectory!, 'YtoolsAM.uic'), 'utf8'), f.xml);
    assert.equal(await readFile(f.manifestPath, 'utf8'), f.manifest);
    assert((await readFile(f.customPath, 'utf8')).includes('DirectOverwrite'));
    assert.equal((await f.workspace.inventory(f.monPath, context.manifestName)).rows.find(r => r.name === 'Ytools')!.origin, 'managed');
    const official = await f.workspace.import({ path: join(f.install, 'design.uic') });
    await assert.rejects(f.workspace.preview({ ...context, project: official.project }), (e: any) => e.status === 403);
  } finally { await clean(f.root); }
});
