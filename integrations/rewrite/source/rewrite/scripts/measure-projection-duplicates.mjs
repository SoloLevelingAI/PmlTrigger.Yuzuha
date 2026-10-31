// Measure repeated API prose, excluding navigation, signatures and generated record boilerplate.
import {readFileSync, readdirSync, writeFileSync} from 'node:fs';
import {resolve, join} from 'node:path';
import {createRequire} from 'node:module';

const [mode, directory, output] = process.argv.slice(2);
const normalize = text => String(text ?? '').replace(/\[([^\]]+)\]\(xref:[\s\S]*\)/g, '$1').replace(/<[^>]*>/g, '').replace(/&(?:#(\d+)|#x([\da-f]+)|([a-z]+));/gi,
    (match, decimal, hex, named) => decimal ? String.fromCodePoint(Number(decimal)) : hex ? String.fromCodePoint(parseInt(hex, 16)) :
        ({amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' '}[named] ?? match)).replace(/\s+/g, ' ').trim();
function summarize(records) {
    const groups = new Map();
    for (const record of records) {
        if (!record.text || /^(编译器生成的记录成员|编译器提供的隐式构造函数)/.test(record.text)) continue;
        const entries = groups.get(record.text) ?? [];
        entries.push(record); groups.set(record.text, entries);
    }
    const repeated = [...groups].filter(([, entries]) => entries.length > 1).map(([text, entries]) => ({
        text, occurrences: entries.length, excess: entries.length - 1,
        repeatedCharacters: [...text.replace(/\s/g, '')].length * (entries.length - 1),
        locations: entries.map(entry => entry.location),
    })).sort((a, b) => b.repeatedCharacters - a.repeatedCharacters || a.text.localeCompare(b.text));
    return {expressions: [...groups.values()].reduce((sum, entries) => sum + entries.length, 0),
        uniqueExpressions: groups.size, repeatedExpressions: repeated.reduce((sum, item) => sum + item.excess, 0),
        repeatedCharacters: repeated.reduce((sum, item) => sum + item.repeatedCharacters, 0), repeated};
}
function snapshot(path) {
    const documentRoot = resolve(path);
    const YAML = createRequire(join(documentRoot, '../../package.json'))('yaml');
    const records = [];
    for (const file of readdirSync(join(documentRoot, 'api')).filter(file => file.endsWith('.yml') && file !== 'toc.yml')) {
        const document = YAML.parse(readFileSync(join(documentRoot, 'api', file), 'utf8'));
        for (const item of document.items ?? []) {
            const add = (kind, text) => records.push({location: item.uid + ':' + kind, text: normalize(text)});
            add('summary', item.summary); add('remarks', item.remarks);
            for (const parameter of item.syntax?.parameters ?? []) add('parameter.' + parameter.id, parameter.description);
            add('returns', item.syntax?.return?.description);
        }
    }
    const rendered = [], tables = [];
    for (const file of readdirSync(join(documentRoot, 'site/api')).filter(file => file.endsWith('.html'))) {
        const html = readFileSync(join(documentRoot, 'site/api', file), 'utf8');
        for (const match of html.matchAll(/<div class="markdown(?: (summary|remarks))?">([\s\S]*?)<\/div>/g))
            rendered.push({location: file + ':' + (match[1] ?? 'member'), text: normalize(match[2])});
        for (const table of html.matchAll(/<table class="api-table (parameters-table|return-table)">([\s\S]*?)<\/table>/g))
            for (const row of table[2].matchAll(/<tr>([\s\S]*?)<\/tr>/g)) {
                const cells = [...row[1].matchAll(/<td\b[^>]*>([\s\S]*?)<\/td>/g)];
                if (cells.length) tables.push({location: file + ':' + table[1], text: normalize(cells.at(-1)[1])});
            }
    }
    return {definition: 'Exact normalized prose; every occurrence after the first counts as repetition. Whitespace earns no savings.',
        metadata: summarize(records), rendered: summarize(rendered), renderedWithContractTables: summarize([...rendered, ...tables])};
}
let result;
if (mode === 'snapshot') result = snapshot(directory);
else if (mode === 'compare') {
    const before = JSON.parse(readFileSync(directory, 'utf8')), after = JSON.parse(readFileSync(output, 'utf8'));
    result = {};
    for (const scope of ['metadata', 'rendered', 'renderedWithContractTables'].filter(scope => before[scope] && after[scope])) {
        const old = before[scope], current = after[scope];
        result[scope] = {before: old.repeatedExpressions, after: current.repeatedExpressions,
            removed: old.repeatedExpressions - current.repeatedExpressions,
            repeatedCharactersBefore: old.repeatedCharacters, repeatedCharactersAfter: current.repeatedCharacters,
            repeatedCharactersRemoved: old.repeatedCharacters - current.repeatedCharacters,
            characterReductionPercent: old.repeatedCharacters ? (old.repeatedCharacters - current.repeatedCharacters) / old.repeatedCharacters * 100 : 0};
    }
} else throw new Error('Use snapshot <artifacts/docfx> <output.json>, or compare <before.json> <after.json>.');
if (mode === 'snapshot') writeFileSync(output, JSON.stringify(result, null, 2) + '\n');
else console.log(JSON.stringify(result, null, 2));
