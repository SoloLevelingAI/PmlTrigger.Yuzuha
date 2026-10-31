import {test} from 'node:test';
import assert from 'node:assert/strict';
import {projectContractReferences} from './project-contracts.mjs';

function fixture() {
    return [{items: [
        {uid: 'IPort', type: 'Interface'},
        {uid: 'IPort.Read(ct)', parent: 'IPort', type: 'Method', summary: '读取；保留原结果。', syntax: {
            parameters: [{id: 'ct', type: 'Token', description: '取消等待，不撤销已受理操作。'}],
            return: {type: 'Result', description: '成功值或脱敏失败。'}}},
        {uid: 'Adapter.Read(ct)', type: 'Method', implements: ['IPort.Read(ct)'], summary: '实现自己的附加说明。', syntax: {
            parameters: [{id: 'ct', type: 'Token', description: '取消等待，不撤销已受理操作。'}],
            return: {type: 'Result', description: '成功值或脱敏失败。'}}},
    ]}];
}

test('links only identical inherited fields and retains the complete contract and implementation summary', () => {
    const documents = fixture(), contract = structuredClone(documents[0].items[1]);
    const report = projectContractReferences(documents);
    assert.equal(report.sharedFields, 2);
    assert.deepEqual(documents[0].items[1], contract);
    const implementation = documents[0].items[2];
    assert.equal(implementation.summary, '实现自己的附加说明。');
    assert.equal(implementation.syntax.parameters[0].description, '<a class="xref" href="IPort.html#IPort_Read_ct_">共享契约参数</a>');
    assert.equal(report.references[0].fields[0].text, contract.syntax.parameters[0].description);
    assert.equal(projectContractReferences(documents).sharedFields, 0);
});

test('keeps differing text, parameter names and types, and skips ambiguous or missing contracts', () => {
    for (const change of [
        item => item.syntax.parameters[0].description = '实现额外限定的取消语义。',
        item => item.syntax.parameters[0].id = 'different',
        item => item.syntax.parameters[0].type = 'OtherToken',
    ]) {
        const documents = fixture(); change(documents[0].items[2]);
        const before = structuredClone(documents[0].items[2].syntax.parameters);
        projectContractReferences(documents);
        assert.deepEqual(documents[0].items[2].syntax.parameters, before);
    }
    for (const targets of [['IPort.Read(ct)', 'Other.Read(ct)'], ['Missing.Read(ct)']]) {
        const documents = fixture(); documents[0].items[2].implements = targets;
        assert.equal(projectContractReferences(documents).sharedFields, 0);
    }
});
