// Compare actual source characters with a Git baseline. Whitespace earns no savings.
import {execFileSync} from 'node:child_process';
import {readFileSync, existsSync} from 'node:fs';
import {dirname, resolve} from 'node:path';
import {fileURLToPath} from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const [baseline = '2733f53', renamed = '0'] = process.argv.slice(2);
const renamedIdentifierSavings = Number(renamed);
if (!Number.isFinite(renamedIdentifierSavings) || renamedIdentifierSavings < 0)
    throw new Error('Identifier savings must be a nonnegative character count.');
const git = (...args) => execFileSync('git', args, {cwd: root, encoding: 'utf8', maxBuffer: 16 * 1024 * 1024});
const tracked = git('ls-tree', '-r', '--name-only', '-z', baseline, '--', 'rewrite').split('\0').filter(Boolean);
const current = [...new Set(git('ls-files', '--cached', '--others', '--exclude-standard', '-z', '--', 'rewrite').split('\0').filter(Boolean))];
const baselineTexts = new Map();
const readBaseline = path => {
    if (!baselineTexts.has(path)) baselineTexts.set(path, git('show', `${baseline}:${path}`));
    return baselineTexts.get(path);
};
const groups = {
    production: path => path.endsWith('.cs') && !path.split('/')[1].endsWith('.Tests'),
    tests: path => path.endsWith('.cs') && path.split('/')[1].endsWith('.Tests'),
    implementation: path => /\.(cs|xaml|csproj|props|slnx|json|txt)$/.test(path),
    audit: path => /\.(md|mjs)$/.test(path),
};
function measure(paths, read, include) {
    const counts = {files: 0, characters: 0, nonWhitespace: 0, xmlCommentLines: 0};
    for (const path of paths.filter(include)) {
        const text = read(path)?.replace(/^\uFEFF/, '').replace(/\r\n/g, '\n');
        if (text === undefined) continue;
        counts.files++;
        counts.characters += [...text].length;
        counts.nonWhitespace += [...text.replace(/\s/g, '')].length;
        if (path.endsWith('.cs')) counts.xmlCommentLines += (text.match(/^\s*\/\/\/[^\n]*/gm) ?? []).length;
    }
    return counts;
}
const result = {baseline: git('rev-parse', baseline).trim(), identifierWeight: 0.2, renamedIdentifierSavings};
for (const [name, include] of Object.entries(groups)) {
    const before = measure(tracked, readBaseline, include);
    const after = measure(current, path => existsSync(resolve(root, path)) ? readFileSync(resolve(root, path), 'utf8') : undefined, include);
    const saved = before.nonWhitespace - after.nonWhitespace;
    result[name] = {before, after, saved, percent: before.nonWhitespace ? saved / before.nonWhitespace * 100 : null};
}
// R is the reviewed total savings caused only by shorter identifier spellings.
// Correct the overall saving S to S - R + 0.2R; do not count renames twice.
result.weightedImplementationSavings = result.implementation.saved - 0.8 * renamedIdentifierSavings;
result.wholeTreeSavingsIncludingAudit = result.implementation.saved + result.audit.saved;
console.log(JSON.stringify(result, null, 2));
