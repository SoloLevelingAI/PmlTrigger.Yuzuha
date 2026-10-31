import {readFileSync, writeFileSync, mkdirSync} from 'node:fs';
import {join} from 'node:path';
import assert from 'node:assert/strict';
import {sourceLinks} from './file-index.mjs';

const clean = uid => uid.replace(/`+\d+/g, '');
const typeId = item => [item.namespace, item.owner, item.name].filter(Boolean).join('.');
const ownerId = item => [item.namespace, item.owner].filter(Boolean).join('.');
const cell = text => String(text).replace(/\|/g, '\\|').replace(/\r?\n/g, ' ');

export function buildFeatureLocator({modules, reading, declarations, tests}) {
    const topics = modules.map(module => {
        const route = reading[module.id];
        assert.ok(route, 'Missing feature route: ' + module.id);
        assert.ok(route.entries?.length && route.startHere?.length, 'Missing feature location: ' + module.id);
        assert.ok(module.name?.trim() && module.description?.trim() && route.result?.trim() && route.flow,
            'Feature purpose or boundary missing: ' + module.id);
        const points = [];
        for (const entry of route.entries) {
            const found = declarations.filter(item => item.kind === 'MethodDeclaration' &&
                clean(ownerId(item)) === clean(entry.type) && item.name === entry.method);
            assert.equal(found.length, 1, 'Missing or ambiguous feature entry: ' + entry.type + '.' + entry.method);
            points.push({...found[0], type: entry.type, label: entry.type.split('.').at(-1) + '.' + entry.method, role: '入口'});
        }
        for (const start of route.startHere) {
            const found = declarations.filter(item => /^(?:Class|Interface|Struct|Record|RecordStruct)Declaration$/.test(item.kind) && clean(typeId(item)) === clean(start.uid));
            assert.ok(found.length, 'Missing feature implementation: ' + start.uid);
            const primary = found.filter(item => item.bases?.length);
            assert.ok(found.length === 1 || primary.length === 1, 'Ambiguous partial type location: ' + start.uid);
            const declaration = primary[0] ?? found[0];
            points.push({...declaration, type: start.uid, label: start.uid.split('.').at(-1), role: '主声明'});
        }
        const byFile = new Map();
        for (const point of points) {
            const existing = byFile.get(point.file) ?? [];
            if (!existing.some(item => item.type === point.type && item.line === point.line)) existing.push(point);
            byFile.set(point.file, existing);
        }
        const related = tests.filter(test => test.modules.includes(module.id));
        return {id: module.id, name: module.name, purpose: module.description, keywords: module.keywords ?? [],
            result: route.result, flow: route.flow, entries: points.filter(point => point.role === '入口'),
            files: [...byFile].map(([file, points]) => ({file, points})), tests: related.map(test => ({id: test.id, url: test.url}))};
    });
    const usage = new Map();
    for (const topic of topics) for (const file of topic.files) {
        const uses = usage.get(file.file) ?? [];
        uses.push({feature: topic.id, types: [...new Set(file.points.map(point => point.type))]}); usage.set(file.file, uses);
    }
    const shared = [...usage].filter(([, uses]) => uses.length > 1).map(([file, uses]) => ({file, uses,
        kind: file.includes('/LaunchDesk.OperationContracts/') ? '共享契约' :
            new Set(uses.flatMap(use => use.types)).size === 1 ? '共享实现' : '混合实现，待评估归属'}));
    return {topics, shared, metrics: {features: topics.length, locatedFeatures: topics.filter(topic => topic.files.length && topic.entries.length).length,
        resolvedEntries: topics.reduce((sum, topic) => sum + topic.entries.length, 0),
        featuresWithRelatedTests: topics.filter(topic => topic.tests.length).length,
        navigationEdgesFromSourceIndexToSource: 2,
        mixedImplementationFiles: shared.filter(item => item.kind.startsWith('混合')).length}};
}

export function writeFeatureLocator({root, graph}) {
    const configuration = JSON.parse(readFileSync(join(root, 'docs/reference/modules.json'), 'utf8'));
    const reading = JSON.parse(readFileSync(join(root, 'docs/reference/reading-map.json'), 'utf8'));
    const declarations = JSON.parse(readFileSync(join(root, 'artifacts/docfx/declarations.json'), 'utf8'));
    const locator = buildFeatureLocator({modules: configuration.modules, reading: reading.modules, declarations, tests: graph.tests});
    const source = sourceLinks(root, graph.sourceRevision);
    const lines = ['# 按功能定位',
        '目标：先说明功能，再从入口定位源码、差异与验证。用途和入口复用现有功能定义，路径取自本次编译声明；位置变化自动更新，不另写多份事实。',
        '验收同时看关键事实、维护点、定位跳转及混合文件；减少字数不能删约束。下表定位 rewrite 候选实现；[范围及分层](index.md)。关联测试不等于覆盖率或当前通过结果。'];
    for (const topic of locator.topics) {
        const paths = ['| 路径（相对仓库根） | 入口／主要声明 |', '| --- | --- |',
            ...topic.files.map(file => '| `' + file.file + '` | ' + file.points.map(point => {
                const count = readFileSync(join(root, file.file), 'utf8').split(/\r?\n/).length;
                assert.ok(Number.isInteger(point.line) && point.line > 0 && point.line <= count, 'Stale feature line: ' + point.file);
                return '[' + cell(point.role + '：' + point.label) + '](' + source(file.file) + '#L' + point.line + ')';
            }).join('<br>') + ' |')].join('\n');
        lines.push('## ' + topic.name, '**查找词**：' + topic.keywords.join('、'), '**功能**：' + topic.purpose, paths,
            '**差异／结果**：' + topic.result,
            '[流程与约束](~/docs/reference/' + topic.flow.replace(/\.html$/, '.md') + ') · ' +
                (topic.tests.length ? '[关联测试 ' + topic.tests.length + ' 项](../tests/index.md?module=' + encodeURIComponent(topic.id) + ')' : '测试关联待补'));
    }
    const shared = ['| 文件 | 功能归属 | 分类 |', '| --- | --- |',
        ...locator.shared.map(item => '| [' + item.file + '](' + source(item.file) + ') | ' + item.uses.map(use => use.feature).join('、') + ' | ' + item.kind + ' |')].join('\n');
    lines.push('## 共性与混合位置', '公共协议见[流程约束](~/docs/reference/articles/flows/index.md)。混合实现只列整理候选，不据静态目录推断调用图或贸然移动 UI。', shared);
    const output = join(root, 'artifacts/docfx/navigation/source-index'); mkdirSync(output, {recursive: true});
    writeFileSync(join(output, 'features.md'), lines.join('\n\n') + '\n');
    writeFileSync(join(root, 'artifacts/docfx/feature-locator.json'), JSON.stringify({...locator, sourceRevision: graph.sourceRevision}, null, 2));
    return locator.metrics;
}
