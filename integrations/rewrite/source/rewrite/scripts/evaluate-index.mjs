import {readFileSync, writeFileSync, existsSync} from 'node:fs';
import {resolve, dirname, join, posix} from 'node:path';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
import {inventory, measureFileIndex} from './file-index.mjs';
import {buildFeatureLocator} from './feature-locator.mjs';

const desktop = 'rewrite/LaunchDesk.Desktop/';
const count = text => [...text.replace(/\s/g, '')].length;
const read = (root, path) => JSON.parse(readFileSync(join(root, path), 'utf8'));

export function desktopStructure({files, declarations, resourceRoles = []}) {
    const roles = new Map();
    for (const item of declarations.filter(item => item.file.startsWith(desktop) && !item.owner && /Declaration$/.test(item.kind))) {
        const role = /ViewModel$/.test(item.name) ? 'ViewModels' : /Command$|Converter$/.test(item.name) ? 'Presentation' :
            /View$/.test(item.name) || item.bases?.some(base => /^(?:Window|ContentControl|FrameworkElement)$/.test(base)) ? 'Views' : null;
        if (!role) continue;
        const entries = roles.get(item.file) ?? new Set(); entries.add(role); roles.set(item.file, entries);
    }
    const mixedFiles = [...roles].filter(([, roles]) => roles.size > 1).map(([file, roles]) => ({file, roles: [...roles]}));
    const misplacedDeclarations = [...roles].flatMap(([file, roles]) => [...roles].filter(role => !file.startsWith(desktop + role + '/')).map(role => ({file, expectedDirectory: role})));
    const looseFiles = files.filter(file => file.startsWith(desktop) && !file.slice(desktop.length).includes('/') &&
        !/\.(?:csproj|md)$/.test(file) && !file.endsWith('/GlobalUsings.cs'));
    const misplacedResources = resourceRoles.filter(({file, role}) => !file.startsWith(desktop + role + '/'));
    return {mixedFiles, misplacedDeclarations, looseFiles, misplacedResources};
}

// Count actual Markdown links. A hop is a document edge, not elapsed lookup time.
export function sourceDistances(pages, targets) {
    const edges = new Map(Object.entries(pages).map(([page, text]) => [page, [...text.matchAll(/\[[^\]]+\]\(([^)]+)\)/g)].map(match => {
        const href = match[1];
        return /^https?:/.test(href) ? href : posix.normalize(posix.join(posix.dirname(page), href));
    })]));
    const queue = [['index.md', 0]], distances = new Map(queue);
    for (let i = 0; i < queue.length; i++) {
        const [page, distance] = queue[i];
        for (const next of edges.get(page) ?? []) if (!distances.has(next)) { distances.set(next, distance + 1); queue.push([next, distance + 1]); }
    }
    const found = targets.map(target => distances.get(target) ?? null);
    return {entries: targets.length, reachable: found.filter(distance => distance !== null).length,
        maximumHops: found.some(distance => distance === null) ? null : Math.max(0, ...found)};
}

export function duplicateIndexProse(texts) {
    const occurrences = new Map();
    for (const text of texts) for (const paragraph of text.split(/\r?\n\s*\r?\n/)) {
        // File lists, headings and table rows are inventory, not repeated narrative facts.
        if (/^(?:#|\||\*\*)/.test(paragraph.trim()) || /`[^`]+\.[a-z]+`/.test(paragraph)) continue;
        const normalized = paragraph.replace(/\[([^\]]+)\]\([^)]+\)/g, '$1').replace(/\s/g, '');
        if (count(normalized) >= 12) occurrences.set(normalized, (occurrences.get(normalized) ?? 0) + 1);
    }
    return [...occurrences].reduce((sum, [text, copies]) => sum + count(text) * (copies - 1), 0);
}

export function evaluateIndex({root, graph}) {
    const indexed = inventory(root), compression = measureFileIndex(root);
    const declarations = read(root, 'artifacts/docfx/declarations.json');
    const configuration = read(root, 'docs/reference/modules.json'), reading = read(root, 'docs/reference/reading-map.json');
    const features = buildFeatureLocator({modules: configuration.modules, reading: reading.modules, declarations, tests: graph.tests});
    const pages = Object.fromEntries(['index.md', 'features.md', ...indexed.projects.map(project => project + '.md'), 'scripts.md']
        .map(page => [page, readFileSync(join(root, 'artifacts/docfx/navigation/source-index', page), 'utf8')]));
    const targets = [...pages['features.md'].matchAll(/\[入口：[^\]]+\]\(([^)]+)\)/g)].map(match => match[1]);
    const navigation = sourceDistances(pages, targets);
    const resourceRoles = indexed.sourceFiles.filter(file => file.startsWith(desktop) && /\.(?:xaml|ico)$/.test(file))
        .map(file => ({file, role: file.endsWith('.ico') || /<ResourceDictionary(?:\s|>)/.test(readFileSync(join(root, file), 'utf8')) ? 'Resources' : 'Views'}));
    const structure = desktopStructure({files: indexed.sourceFiles, declarations, resourceRoles});
    const registryPaths = JSON.stringify([configuration.modules, reading.modules]).match(/rewrite[\\/][^"\s]+/g) ?? [];
    const prefixes = new Map();
    for (const reference of indexed.references.filter(item => item.directory)) {
        const key = reference.index + ':' + reference.directory;
        const group = prefixes.get(key) ?? {prefix: reference.directory, files: 0}; group.files++; prefixes.set(key, group);
    }
    const checks = {
        sourceCoverage: indexed.report.missing.length === 0,
        singleFileOwnership: indexed.report.duplicateFileEntries.length === 0,
        allFeaturesLocated: features.metrics.locatedFeatures === features.metrics.features,
        sourceLinksWithinTwoHops: targets.length === features.metrics.resolvedEntries && navigation.reachable === targets.length && navigation.maximumHops <= 2,
        desktopResponsibilitiesSeparated: structure.mixedFiles.length === 0 && structure.misplacedDeclarations.length === 0,
        desktopRootAndResourcesClassified: structure.looseFiles.length === 0 && structure.misplacedResources.length === 0,
        noHandwrittenFeatureSourcePaths: registryPaths.length === 0,
        authoredTextWithinTarget: compression.reductionPercent >= 50
    };
    return {passed: Object.values(checks).every(Boolean), checks,
        objective: 'First preserve facts and pass every structural gate; then compare text, duplicate maintenance and navigation costs separately. No aggregate density score.',
        coverage: indexed.report, features: features.metrics, navigation, desktop: structure,
        directoryPrefixReuse: {groups: prefixes.size, fileEntries: [...prefixes.values()].reduce((sum, group) => sum + group.files, 0),
            redundantOccurrencesAvoided: [...prefixes.values()].reduce((sum, group) => sum + group.files - 1, 0),
            charactersAvoided: [...prefixes.values()].reduce((sum, group) => sum + count(group.prefix) * (group.files - 1), 0),
            comparison: 'Against spelling the same new directory prefix on every file entry; not historical net text savings.'},
        costs: {authoredCharacters: compression.after, authoredReductionPercent: compression.reductionPercent,
            authoredReductionTargetPercent: 50, authoredReductionTargetMet: compression.reductionPercent >= 50,
            generatedLayeredIndexMarkdownCharacters: Object.entries(pages).filter(([page]) => page !== 'features.md').reduce((sum, [, text]) => sum + count(text), 0),
            generatedLayeredIndexCharactersWithoutLinkTargets: Object.entries(pages).filter(([page]) => page !== 'features.md').reduce((sum, [, text]) => sum + count(text.replace(/\[([^\]]+)\]\([^)]+\)/g, '$1')), 0),
            generatedFeatureMarkdownCharacters: count(pages['features.md']),
            generatedFeatureCharactersWithoutLinkTargets: count(pages['features.md'].replace(/\[([^\]]+)\]\([^)]+\)/g, '$1')),
            repeatedIndexNarrativeCharacters: duplicateIndexProse(indexed.entries.map(entry => entry.text)),
            handwrittenFeatureSourcePaths: registryPaths.length},
        interpretation: ['Compression counts Unicode non-whitespace characters, not lines.', 'Repeated narrative counts identical paragraphs of at least 12 characters; file lists, headings and tables are excluded. Zero does not prove absence of semantic duplication.',
            'Generated feature text is additional display cost, not claimed compression savings.', 'Directory checks cover Desktop; backend mixed files remain separately visible in the feature locator.', 'Related tests are associations, not coverage or a current pass result.'],
        sourceRevision: graph.sourceRevision};
}

export function writeIndexEvaluation({root, graph}) {
    const report = evaluateIndex({root, graph});
    writeFileSync(join(root, 'artifacts/docfx/file-index-evaluation.json'), JSON.stringify(report, null, 2));
    assert.ok(report.passed, 'INDEX evaluation failed: ' + Object.entries(report.checks).filter(([, passed]) => !passed).map(([name]) => name).join(', '));
    return report;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
    const path = join(root, 'artifacts/docfx/navigation/graph.json');
    assert.ok(existsSync(path), 'Build documentation before evaluating INDEX navigation.');
    const report = writeIndexEvaluation({root, graph: read(root, 'artifacts/docfx/navigation/graph.json')});
    console.log(JSON.stringify(report, null, 2));
}
