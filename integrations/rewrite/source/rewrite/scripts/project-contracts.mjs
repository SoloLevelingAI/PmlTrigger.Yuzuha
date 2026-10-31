// Keep shared contract prose at its interface declaration; implementation-specific prose stays local.
import {readFileSync, readdirSync, existsSync} from 'node:fs';
import {join, resolve, sep} from 'node:path';
import assert from 'node:assert/strict';

const same = (left, right) => typeof left === 'string' && left.trim() &&
    left.replace(/\s+/g, ' ').trim() === right?.replace(/\s+/g, ' ').trim();
// Metadata has already passed DocFX's markup stage. Emit resolved HTML links, not raw Markdown.
const link = (label, contract) => `<a class="xref" href="${contract.parent.replaceAll('`', '-')}.html#${contract.uid.replace(/[^A-Za-z0-9_]/g, '_')}">${label}</a>`;

export function projectContractReferences(documents) {
    const items = new Map(documents.flatMap(document => document.items ?? []).map(item => [item.uid, item]));
    const report = [];
    for (const item of items.values()) {
        if (item.type !== 'Method' || item.implements?.length !== 1) continue;
        const contract = items.get(item.implements[0]);
        if (!contract || items.get(contract.parent)?.type !== 'Interface') continue;
        const fields = [];
        for (const parameter of item.syntax?.parameters ?? []) {
            const original = contract.syntax?.parameters?.find(candidate => candidate.id === parameter.id && candidate.type === parameter.type);
            if (!original || !same(parameter.description, original.description)) continue;
            fields.push({field: 'parameter.' + parameter.id, text: parameter.description});
            parameter.description = link('共享契约参数', contract);
        }
        const result = item.syntax?.return, original = contract.syntax?.return;
        if (result?.type === original?.type && same(result?.description, original?.description)) {
            fields.push({field: 'returns', text: result.description});
            result.description = link('共享契约返回值', contract);
        }
        if (fields.length) report.push({implementation: item.uid, contract: contract.uid, fields});
    }
    return {methods: report.length, sharedFields: report.reduce((sum, entry) => sum + entry.fields.length, 0), references: report};
}

export function checkProjectedContractLinks(directory) {
    const site = resolve(directory, 'site');
    const report = JSON.parse(readFileSync(join(directory, 'shared-contracts.json'), 'utf8'));
    let checked = 0;
    for (const file of readdirSync(join(site, 'api')).filter(file => file.endsWith('.html'))) {
        const html = readFileSync(join(site, 'api', file), 'utf8');
        assert.ok(!/\[共享契约(?:参数|返回值)\]\(xref:/.test(html), 'Unrendered contract link in ' + file);
        for (const anchor of html.matchAll(/<a\b([^>]*)>([\s\S]*?)<\/a>/g)) {
            if (!/^共享契约(?:参数|返回值)$/.test(anchor[2].replace(/<[^>]*>/g, '').trim())) continue;
            const href = anchor[1].match(/href="([^"]+)"/)?.[1]; assert.ok(href);
            const url = new URL(href.replaceAll('&amp;', '&'), 'https://reference.local/api/' + file);
            assert.equal(url.origin, 'https://reference.local');
            const target = resolve(site, '.' + decodeURIComponent(url.pathname));
            assert.ok(target.startsWith(site + sep) && existsSync(target), 'Missing contract page: ' + href);
            assert.ok(url.hash.length > 1 && readFileSync(target, 'utf8').includes('id="' + decodeURIComponent(url.hash.slice(1)) + '"'), 'Missing contract anchor: ' + href);
            checked++;
        }
    }
    // DocFX may also inherit a link by position when an implementation renames a parameter.
    assert.ok(checked >= report.sharedFields, 'Contract fields missing from rendered pages');
    return {checkedContractLinks: checked, additionalInheritedLinks: checked - report.sharedFields};
}

if (process.argv[2] === 'check') console.log(JSON.stringify(checkProjectedContractLinks(resolve(process.argv[3]))));
