import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, writeFile, readFile, mkdir, readdir, rm } from 'node:fs/promises';
import { resolve, join, basename } from 'node:path';
import { create } from 'xmlbuilder2';
import { exampleProject } from '../packages/uic-core/example.ts';
import { previewDeployment, deployUic, inspectInstallation, DeploymentError } from '../packages/uic-deployment/index.ts';

const manifest = '<?xml version="1.0" encoding="utf-8"?>\r\n<UICustomizationSet>\r\n  <!-- existing user settings -->\r\n  <UICustomizationFiles>\r\n    <CustomizationFile Name="Module" Path="design.uic"/>\r\n  </UICustomizationFiles>\r\n</UICustomizationSet>\r\n';
async function fixture() {
  await mkdir(resolve('.runtime'), { recursive: true });
  const directory = await mkdtemp(resolve('.runtime/deployment-test-'));
  const monPath = join(directory, 'mon.exe'), manifestPath = join(directory, 'DesignCustomization.xml');
  await writeFile(monPath, 'Disposable test launcher; not executable.');
  await writeFile(manifestPath, manifest);
  return { directory, monPath, manifestPath };
}
test('deployment preview is read-only; apply backs up, registers once, verifies, and updates deterministically', async () => {
  const f = await fixture();
  try {
    const project = await exampleProject();
    const input = { project, monPath: f.monPath, manifestName: 'DesignCustomization.xml' };
    const target = await inspectInstallation(f.monPath);
    assert.deepEqual(target.manifests, ['DesignCustomization.xml']);
    const plan = await previewDeployment(input);
    assert.equal(await readFile(f.manifestPath, 'utf8'), manifest);
    assert.deepEqual((await readdir(f.directory)).sort(), ['DesignCustomization.xml', 'mon.exe']);
    const result = await deployUic({ ...input, expectedManifestHash: plan.manifestHash, expectedUicHash: plan.uicHash });
    assert(result.backupDirectory);
    assert.equal(await readFile(join(result.backupDirectory!, 'DesignCustomization.xml'), 'utf8'), manifest);
    const next = await readFile(f.manifestPath, 'utf8');
    assert(next.includes('<!-- existing user settings -->'));
    assert(next.includes('    <CustomizationFile Name="Module" Path="design.uic"/>'));
    const entries = (create(next).node as any).documentElement.getElementsByTagName('CustomizationFile');
    assert.equal(entries.length, 2);
    assert.equal(entries[1].getAttribute('Path'), basename(plan.uicPath));
    assert((await readFile(plan.uicPath, 'utf8')).includes('<UserInterfaceCustomization'));
    const repeat = await previewDeployment(input);
    assert.equal(repeat.unchanged, true);
    const noOp = await deployUic({ ...input, expectedManifestHash: repeat.manifestHash, expectedUicHash: repeat.uicHash });
    assert.equal(noOp.backupDirectory, null);
    const oldUic = await readFile(plan.uicPath);
    project.buttons[0].macro = 'show !!UpdatedForm';
    const updatePlan = await previewDeployment(input);
    const updated = await deployUic({ ...input, expectedManifestHash: updatePlan.manifestHash, expectedUicHash: updatePlan.uicHash });
    assert.deepEqual(await readFile(join(updated.backupDirectory!, basename(plan.uicPath))), oldUic);
    assert.equal((await readdir(join(f.directory, '.ribbon-studio-backups'))).length, 2);
    assert.equal((create(await readFile(f.manifestPath, 'utf8')).node as any).documentElement.getElementsByTagName('CustomizationFile').length, 2);
  } finally { await rm(f.directory, { recursive: true, force: true }); }
});
test('stale preview, unrelated UIC files, invalid target paths and malformed manifests cannot overwrite files', async () => {
  const f = await fixture();
  try {
    const input = { project: await exampleProject(), monPath: f.monPath, manifestName: 'DesignCustomization.xml' };
    const plan = await previewDeployment(input);
    await writeFile(f.manifestPath, manifest + '<!-- external change -->');
    await assert.rejects(deployUic({ ...input, expectedManifestHash: plan.manifestHash, expectedUicHash: null }), (e: any) => e instanceof DeploymentError && e.status === 409);
    assert(!(await readdir(f.directory)).includes(basename(plan.uicPath)));
    await writeFile(f.manifestPath, manifest);
    await writeFile(plan.uicPath, 'unrelated content');
    await assert.rejects(previewDeployment(input), (e: any) => e.status === 409);
    assert.equal(await readFile(plan.uicPath, 'utf8'), 'unrelated content');
    await assert.rejects(previewDeployment({ ...input, manifestName: '../DesignCustomization.xml' }), (e: any) => e.status === 422);
    await assert.rejects(inspectInstallation(join(f.directory, 'missing.exe')), (e: any) => e.status === 422);
    await writeFile(f.manifestPath, '<not-a-customization-file/>');
    await assert.rejects(previewDeployment(input), (e: any) => e.status === 422);
  } finally { await rm(f.directory, { recursive: true, force: true }); }
});
test('UTF-16 BOM, CRLF and self-closing customization lists are preserved on deployment', async () => {
  const f = await fixture();
  try {
    const text = '<?xml version="1.0" encoding="utf-16"?>\r\n<UICustomizationSet>\r\n  <!-- 中文 -->\r\n  <UICustomizationFiles/>\r\n</UICustomizationSet>\r\n';
    const original = Buffer.concat([Buffer.from([255, 254]), Buffer.from(text, 'utf16le')]);
    await writeFile(f.manifestPath, original);
    const input = { project: await exampleProject(), monPath: f.monPath, manifestName: 'DesignCustomization.xml' };
    const p = await previewDeployment(input);
    const result = await deployUic({ ...input, expectedManifestHash: p.manifestHash, expectedUicHash: null });
    assert.deepEqual(await readFile(join(result.backupDirectory!, 'DesignCustomization.xml')), original);
    const bytes = await readFile(f.manifestPath);
    assert.deepEqual(bytes.subarray(0, 2), Buffer.from([255, 254]));
    assert(bytes.subarray(2).toString('utf16le').includes('<!-- 中文 -->\r\n'));
  } finally { await rm(f.directory, { recursive: true, force: true }); }
});
