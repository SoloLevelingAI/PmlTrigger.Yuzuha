import {readFileSync, readdirSync, writeFileSync, mkdirSync, existsSync, statSync} from 'node:fs';
import {resolve, join, dirname, relative, sep} from 'node:path';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';

const baseline = '6185aba7afce8f2a54c1a109c01ebe989a6b5539';
const guides = ['docs/rewrite/IMPLEMENTATION.md', ...['architecture','exploring','getting-started','glossary',
    'flows/index','flows/launch','flows/delivery','flows/variables','flows/sharing-migration','flows/monitoring']
    .map(name => 'docs/reference/articles/' + name + '.md')];
const slash = path => path.split(sep).join('/');
const count = text => [...text.replace(/\s/g, '')].length;
const git = (root, ...args) => execFileSync('git', args, {cwd: root, encoding: 'utf8', maxBuffer: 8 * 1024 * 1024}).trimEnd();
function files(directory) {
    return readdirSync(directory, {withFileTypes: true}).filter(entry => !['bin','obj','.idea','TestResults'].includes(entry.name))
        .flatMap(entry => entry.isDirectory() ? files(join(directory, entry.name)) : [join(directory, entry.name)]);
}
// A scoped heading states a directory once; file names underneath stay relative to it.
export function indexTokens(text) {
    let directory = '';
    return text.split(/\r?\n/).map(line => {
        if (/^## /.test(line)) directory = line.match(/^## `([^`]+\/)`/)?.[1] ?? '';
        return {line, directory};
    });
}
function directoryEntries(root, entry, directory) {
    const target = resolve(root, dirname(entry.path), directory);
    assert.ok(target.startsWith(join(root, 'rewrite') + sep) && statSync(target).isDirectory(), 'Invalid inventory directory: ' + directory);
    return files(target).map(path => ({file: slash(relative(root, path)), name: slash(relative(target, path))})).sort((a, b) => a.name.localeCompare(b.name));
}
export function inventory(root) {
    const source = join(root, 'rewrite');
    const projects = Object.keys(JSON.parse(readFileSync(join(source, 'architecture.json'), 'utf8')).projectReferences);
    const indexes = ['rewrite/README.md', ...projects.map(project => 'rewrite/' + project + '/INDEX.md'), 'rewrite/scripts/INDEX.md'];
    const entries = indexes.map(path => ({path, text: readFileSync(join(root, path), 'utf8')}));
    const covered = new Set(), common = new Set(), references = [];
    const overview = entries[0].text;
    assert.ok(overview.includes('各项目同名 `.csproj`') && overview.includes('`GlobalUsings.cs`'), 'Missing shared build rule');
    for (const project of projects) {
        assert.ok(overview.includes('`' + project.replace('LaunchDesk.', '') + '`'), 'Unlisted layer project: ' + project);
        common.add('rewrite/' + project + '/' + project + '.csproj');
        if (existsSync(join(source, project, 'GlobalUsings.cs'))) common.add('rewrite/' + project + '/GlobalUsings.cs');
    }
    for (const entry of entries) {
        if (entry.text.includes('<!-- inventory: directories -->')) for (const {line, directory} of indexTokens(entry.text).filter(item => /^## /.test(item.line) && item.directory)) {
            for (const item of directoryEntries(root, entry, directory)) {
                covered.add(item.file); references.push({file: item.file, index: entry.path, directory, directoryRule: true});
            }
        }
        for (const {line, directory} of indexTokens(entry.text)) for (const token of line.matchAll(/`([^`\n]+)`/g)) {
            if (!/\.(?:cs|xaml|ico|csproj|props|slnx|json|mjs|txt|config)$/i.test(token[1])) continue;
            if (entry.path === 'rewrite/README.md' && ['.csproj','GlobalUsings.cs'].includes(token[1])) continue;
            const target = resolve(root, dirname(entry.path), directory, token[1]);
            assert.ok(target.startsWith(source + sep) && existsSync(target), 'Missing indexed source: ' + token[1]);
            const path = slash(relative(root, target));
            covered.add(path); references.push({file: path, index: entry.path, directory});
        }
        for (const link of entry.text.matchAll(/\[[^\]]+\]\(([^)]+)\)/g)) {
            if (/^(?:https?:|xref:|~\/)/.test(link[1])) continue;
            const target = resolve(root, dirname(entry.path), link[1]);
            assert.ok(existsSync(target), 'Broken index link: ' + entry.path + ' -> ' + link[1]);
            if (target.startsWith(source + sep)) {
                const path = slash(relative(root, target));
                covered.add(path); references.push({file: path, index: entry.path});
            }
        }
    }
    const sourceFiles = files(source).map(path => slash(relative(root, path)))
        .filter(path => !indexes.includes(path));
    const missing = sourceFiles.filter(path => !covered.has(path) && !common.has(path));
    assert.deepEqual(missing, [], 'Source files missing from layered index');
    const duplicates = sourceFiles.filter(file => references.filter(reference => reference.file === file).length > (common.has(file) ? 0 : 1));
    return {entries, projects, sourceFiles, references, report: {indexFiles: entries.length, indexedFiles: sourceFiles.length,
        explicitEntries: sourceFiles.filter(path => references.some(reference => reference.file === path && !reference.directoryRule)).length,
        directoryRuleEntries: sourceFiles.filter(path => references.some(reference => reference.file === path && reference.directoryRule)).length,
        commonBuildEntries: common.size, missing, duplicateFileEntries: duplicates}};
}

export function measureFileIndex(root) {
    const {entries, report} = inventory(root);
    const before = guides.reduce((sum, path) => sum + count(git(root, 'show', baseline + ':' + path)), 0);
    const afterGuides = guides.reduce((sum, path) => sum + count(readFileSync(join(root, path), 'utf8')), 0);
    const afterIndexes = entries.reduce((sum, entry) => sum + count(entry.text), 0);
    return {...report, baseline, before, afterGuides, afterIndexes, after: afterGuides + afterIndexes,
        reductionPercent: (1 - (afterGuides + afterIndexes) / before) * 100,
        metric: 'Unicode non-whitespace characters in 11 authored guides and 13 authored layered indexes; paths included. Generated projections, metadata and tooling excluded; no identifier renames.'};
}

export function sourceLinks(root, revision = git(root, 'rev-parse', 'HEAD')) {
    const origin = git(root, 'config', '--get', 'remote.origin.url').replace(/\.git$/, '')
        .replace(/^git@([^:]+):/, 'https://$1/');
    return path => origin + '/' + (statSync(join(root, path)).isDirectory() ? 'tree' : 'blob') + '/' + revision + '/' + path.split('/').map(encodeURIComponent).join('/');
}

export function writeSourceIndex({root}) {
    const {entries, projects, report} = inventory(root);
    const output = join(root, 'artifacts/docfx/navigation/source-index');
    mkdirSync(output, {recursive: true});
    const revision = git(root, 'rev-parse', 'HEAD');
    const destinations = new Map(entries.map(entry => [entry.path,
        entry.path === 'rewrite/README.md' ? 'index.md' : entry.path.split('/')[1] + '.md']));
    const sourceLink = sourceLinks(root, revision);
    for (const entry of entries) {
        let text = entry.text.replace(/\[([^\]]+)\]\(([^)]+)\)/g, (match, label, href) => {
            if (/^(?:https?:|xref:|~\/)/.test(href)) return match;
            const path = slash(relative(root, resolve(root, dirname(entry.path), href)));
            const target = destinations.get(path) ?? (path.startsWith('docs/reference/articles/') ? '~/' + path : sourceLink(path));
            return '[' + label + '](' + target + ')';
        });
        text = indexTokens(text).flatMap(({line, directory}) => {
            const rendered = line.replace(/`([^`\n]+)`/g, (match, token) => {
            const project = projects.find(project => project.replace('LaunchDesk.', '') === token);
            if (entry.path === 'rewrite/README.md' && project) return '[' + token + '](' + project + '.md)';
            const path = slash(relative(root, resolve(root, dirname(entry.path), directory, token)));
            return existsSync(join(root, path)) && statSync(join(root, path)).isFile() ? '[' + token + '](' + sourceLink(path) + ')' : match;
            });
            return entry.text.includes('<!-- inventory: directories -->') && /^## /.test(line) && directory ?
                [rendered, '', directoryEntries(root, entry, directory).map(item => '[' + item.name + '](' + sourceLink(item.file) + ')').join(' · ')] : [rendered];
        }).join('\n');
        if (entry.path === 'rewrite/README.md') text = text.replace('\n\n', '\n\n[按功能定位](features.md)\n\n');
        if (entry.path !== 'rewrite/README.md') {
            const directory = dirname(entry.path), project = projects.find(project => directory === 'rewrite/' + project);
            const common = project ? [directory + '/' + project + '.csproj', directory + '/GlobalUsings.cs'].filter(path => existsSync(join(root, path))) : [];
            text += '\n[总览](index.md)' + common.map(path => ' · [' + path.split('/').at(-1) + '](' + sourceLink(path) + ')').join('') + '\n';
        }
        writeFileSync(join(output, destinations.get(entry.path)), text);
    }
    writeFileSync(join(root, 'artifacts/docfx/source-index.json'), JSON.stringify({...report, sourceRevision: revision}, null, 2));
    return report;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
    const result = process.argv[2] === 'measure' ? measureFileIndex(root) : inventory(root).report;
    if (process.argv.includes('--require-half')) assert.ok(result.reductionPercent >= 50, 'Documentation compression is below 50%');
    console.log(JSON.stringify(result, null, 2));
}
