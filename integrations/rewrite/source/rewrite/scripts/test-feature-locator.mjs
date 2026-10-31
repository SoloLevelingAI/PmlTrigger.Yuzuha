import {test} from 'node:test';
import assert from 'node:assert/strict';
import {buildFeatureLocator} from './feature-locator.mjs';

function fixture() {
    return {modules: [{id: 'launch', name: '启动', description: '提交并观察独立进程。', keywords: ['登录','启动']}],
        reading: {launch: {entries: [{type: 'Product.IRequests', method: 'Submit'}], startHere: [{uid: 'Product.Handler'}], result: '保留原号。', flow: 'articles/flows/launch.html'}},
        declarations: [
            {kind: 'MethodDeclaration', namespace: 'Product', owner: 'IRequests', name: 'Submit', file: 'Ports.cs', line: 7},
            {kind: 'ClassDeclaration', namespace: 'Product', owner: '', name: 'Handler', file: 'Helpers.cs', line: 4, bases: []},
            {kind: 'ClassDeclaration', namespace: 'Product', owner: '', name: 'Handler', file: 'Handler.cs', line: 2, bases: ['Base']}],
        tests: [{id: 'test:launch', url: 'reference/tests/test-launch.html', modules: ['launch']}]};
}

test('resolves names to actual entries, primary partial declarations and test evidence without new path facts', () => {
    const result = buildFeatureLocator(fixture());
    assert.deepEqual(result.topics[0].keywords, ['登录','启动']);
    assert.deepEqual(result.topics[0].files.map(item => item.file), ['Ports.cs','Handler.cs']);
    assert.equal(result.topics[0].entries[0].line, 7);
    assert.equal(result.metrics.featuresWithRelatedTests, 1);
    const absent = fixture(); absent.tests = [];
    assert.equal(buildFeatureLocator(absent).metrics.featuresWithRelatedTests, 0);
});

test('rejects missing, stale and ambiguous entry mappings', () => {
    const empty = fixture(); empty.reading.launch.entries = [];
    assert.throws(() => buildFeatureLocator(empty), /Missing feature location/);
    const absent = fixture(); absent.reading.launch.entries[0].method = 'Missing';
    assert.throws(() => buildFeatureLocator(absent), /Missing or ambiguous feature entry/);
    const ambiguous = fixture(); ambiguous.declarations.push({...ambiguous.declarations[0], file: 'Other.cs'});
    assert.throws(() => buildFeatureLocator(ambiguous), /Missing or ambiguous feature entry/);
});
