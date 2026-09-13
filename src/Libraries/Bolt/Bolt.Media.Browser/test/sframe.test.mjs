import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { initializeSFrame, SFrameSession } from '../wwwroot/bolt-sframe.mjs';

await initializeSFrame(readFileSync(new URL('../wwwroot/sframe/bolt_sframe_bg.wasm', import.meta.url)));
const payload = Uint8Array.of(1, 2, 3, 4);
const binding = 'a'.repeat(64);
const entry = (senderId, kid, key) => ({ senderId, kid: String(kid), key: new Uint8Array(32).fill(key) });
function setup(call = 'call', revision = 1, names = ['alice', 'bob']) {
    const members = names.map((name, i) => entry(name, revision * 10 + i, revision * 10 + i));
    const sessions = members.map(local => {
        const session = new SFrameSession(call, local.senderId);
        session.installEpoch({ epochId: String(revision), rosterBinding: binding, local,
            remote: members.filter(member => member !== local) });
        session.activateEpoch(String(revision), binding);
        return session;
    });
    return sessions;
}
function close(sessions) { sessions.forEach(session => session.dispose()); }

test('three independent sender keys roundtrip and reject replay', () => {
    const sessions = setup('call', 1, ['alice', 'bob', 'carol']);
    try {
        const packet = sessions[0].encrypt(payload, 'stream', 0, 0);
        for (const receiver of sessions.slice(1)) {
            assert.deepEqual(receiver.decrypt('alice', packet, 'stream', 0, 0), payload);
            assert.throws(() => receiver.decrypt('alice', packet, 'stream', 0, 0));
        }
        const reply = sessions[1].encrypt(payload, 'bob-stream', 0, 0);
        assert.deepEqual(sessions[0].decrypt('bob', reply, 'bob-stream', 0, 0), payload);
    } finally { close(sessions); }
});

test('tamper and wrong routing cannot poison valid frame replay state', () => {
    const sessions = setup();
    try {
        const [tx, rx] = sessions;
        const packet = tx.encrypt(payload, 'stream', 1, 960);
        const tampered = packet.slice(); tampered[tampered.length - 1] ^= 1;
        assert.throws(() => rx.decrypt('alice', tampered, 'stream', 1, 960));
        for (const route of [['other', 1, 960], ['stream', 2, 960], ['stream', 1, 1920]])
            assert.throws(() => rx.decrypt('alice', packet, ...route));
        assert.throws(() => rx.decrypt('mallory', packet, 'stream', 1, 960));
        assert.deepEqual(rx.decrypt('alice', packet, 'stream', 1, 960), payload);
    } finally { close(sessions); }
});

test('cross-call ciphertext transplant fails despite identical test keys', () => {
    const a = setup('call-a'); const b = setup('call-b');
    try {
        const packet = a[0].encrypt(payload, 'stream', 1, 960);
        assert.throws(() => b[1].decrypt('alice', packet, 'stream', 1, 960));
    } finally { close(a); close(b); }
});

test('member removal pauses sending; rotation drops old keys and rejects old frames', () => {
    const sessions = setup('call', 1, ['alice', 'bob', 'carol']);
    try {
        const [alice, bob, carol] = sessions;
        const old = alice.encrypt(payload, 'stream', 1, 960);
        alice.pause(); bob.pause();
        assert.throws(() => alice.encrypt(payload, 'stream', 2, 1920));
        const a = entry('alice', 21, 21); const b = entry('bob', 22, 22);
        alice.installEpoch({ epochId: '2', rosterBinding: binding, local: a, remote: [b] });
        bob.installEpoch({ epochId: '2', rosterBinding: binding, local: b, remote: [a] });
        assert.throws(() => alice.encrypt(payload, 'stream', 2, 1920));
        alice.activateEpoch('2', binding); bob.activateEpoch('2', binding);
        assert.throws(() => bob.decrypt('alice', old, 'stream', 1, 960));
        const current = alice.encrypt(payload, 'stream', 2, 1920);
        assert.deepEqual(bob.decrypt('alice', current, 'stream', 2, 1920), payload);
        assert.throws(() => carol.decrypt('alice', current, 'stream', 2, 1920));
        assert.throws(() => alice.installEpoch({ epochId: '2', rosterBinding: binding, local: a, remote: [b] }));
    } finally { close(sessions); }
});

test('key reuse, shared sender key, bounds and disposed session fail closed', () => {
    const sessions = setup();
    try {
        const [alice] = sessions;
        assert.throws(() => alice.encrypt(new Uint8Array(4097), 'stream', 0, 0));
        assert.throws(() => alice.decrypt('bob', new Uint8Array(5156), 'stream', 0, 0));
        assert.throws(() => alice.installEpoch({ epochId: 'new', rosterBinding: binding,
            local: entry('alice', 10, 30), remote: [entry('bob', 31, 31)] }));
        assert.throws(() => alice.installEpoch({ epochId: 'new', rosterBinding: binding,
            local: entry('alice', 30, 30), remote: [entry('bob', 31, 30)] }));
        alice.dispose();
        assert.throws(() => alice.encrypt(payload, 'stream', 0, 0));
        assert.throws(() => alice.activateEpoch('1', binding));
    } finally { close(sessions); }
});
