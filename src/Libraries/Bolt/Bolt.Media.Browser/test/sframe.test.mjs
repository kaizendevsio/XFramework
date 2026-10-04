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

function setupCompact(call = 'call', revision = 1, names = ['alice', 'bob'], compact = names.map(() => true)) {
    const members = names.map((name, i) => entry(name, revision * 10 + i, revision * 10 + i));
    return members.map((local, i) => {
        const session = new SFrameSession(call, local.senderId);
        session.installEpoch({ epochId: String(revision), rosterBinding: binding, local,
            remote: members.filter(member => member !== local), compact: compact[i] });
        session.activateEpoch(String(revision), binding);
        return session;
    });
}

test('a compact epoch adds at most 20 bytes a frame; the legacy one hundreds', () => {
    // Yap's real identifiers: a call UUID and yap-media-<call>-<credential> senders.
    const call = '0f8fad5b-d9cb-469f-a165-70867728950e', sender = 'yap-media-' + 'a'.repeat(32) + '-' + 'b'.repeat(32);
    const legacy = setup(call, 1, [sender, 'bob']);
    const compact = setupCompact(call, 1, [sender, 'bob']);
    try {
        const opus = new Uint8Array(80).fill(7);
        const stream = '16fd2706-8baf-433b-82eb-8c7fada847da';
        const old = legacy[0].encrypt(opus, stream, 123456, 4294967295);
        assert.ok(old.length - opus.length > 250, `legacy overhead ${old.length - opus.length}`);
        for (let sequence = 0; sequence < 600; sequence++) {
            const frame = compact[0].encrypt(opus, stream, sequence, sequence * 960);
            assert.ok(frame.length - opus.length <= 20, `compact overhead ${frame.length - opus.length}`);
            assert.deepEqual(compact[1].decrypt(sender, frame, stream, sequence, sequence * 960), opus);
        }
    } finally { close(legacy); close(compact); }
});

test('receivers take both formats, so a call can mix old and new senders', () => {
    // Alice sends compact frames, Bob (an older sender) legacy ones; each decodes the other.
    const [alice, bob] = setupCompact('call', 1, ['alice', 'bob'], [true, false]);
    try {
        const fromAlice = alice.encrypt(payload, 'a-stream', 1, 960);
        const fromBob = bob.encrypt(payload, 'b-stream', 1, 960);
        assert.ok(fromAlice.length <= payload.length + 20);
        assert.ok(fromBob.length > payload.length + 60, "the legacy context rides inside the ciphertext");
        assert.deepEqual(bob.decrypt('alice', fromAlice, 'a-stream', 1, 960), payload);
        assert.deepEqual(alice.decrypt('bob', fromBob, 'b-stream', 1, 960), payload);
        assert.throws(() => bob.decrypt('alice', fromAlice, 'a-stream', 1, 960), 'replay');
    } finally { close([alice, bob]); }
});

test('a compact frame is bound to its call, epoch, roster, sender, stream, sequence and timestamp', () => {
    const sessions = setupCompact('call', 1, ['alice', 'bob', 'carol']);
    const other = setupCompact('call-b', 1, ['alice', 'bob']);
    try {
        const [alice, bob, carol] = sessions;
        const packet = alice.encrypt(payload, 'stream', 5, 4800);
        for (const route of [['other', 5, 4800], ['stream', 6, 4800], ['stream', 5, 4801]])
            assert.throws(() => bob.decrypt('alice', packet, ...route));
        assert.throws(() => bob.decrypt('carol', packet, 'stream', 5, 4800), 'another sender key');
        assert.throws(() => other[1].decrypt('alice', packet, 'stream', 5, 4800), 'another call');
        const tampered = packet.slice(); tampered[tampered.length - 1] ^= 1;
        assert.throws(() => carol.decrypt('alice', tampered, 'stream', 5, 4800));
        assert.deepEqual(bob.decrypt('alice', packet, 'stream', 5, 4800), payload);
        assert.deepEqual(carol.decrypt('alice', packet, 'stream', 5, 4800), payload);
        // A new epoch: the old epoch's compact frames no longer decrypt.
        alice.pause(); bob.pause();
        const a = entry('alice', 21, 21); const b = entry('bob', 22, 22);
        alice.installEpoch({ epochId: '2', rosterBinding: binding, local: a, remote: [b], compact: true });
        bob.installEpoch({ epochId: '2', rosterBinding: binding, local: b, remote: [a], compact: true });
        alice.activateEpoch('2', binding); bob.activateEpoch('2', binding);
        assert.throws(() => bob.decrypt('alice', packet, 'stream', 5, 4800));
    } finally { close(sessions); close(other); }
});

test('sender key IDs below 8 are reserved for the compact format marker', () => {
    const session = new SFrameSession('call', 'alice');
    try {
        assert.throws(() => session.installEpoch({ epochId: '1', rosterBinding: binding,
            local: entry('alice', 1, 1), remote: [entry('bob', 22, 22)], compact: true }), /key ID/);
    } finally { session.dispose(); }
});

test("a picture's orientation travels encrypted, and no change to it on the way decrypts", () => {
    // A version 2 video fragment (VideoFrameFragments.Split): a 90° keyframe, one fragment, frame 7, timestamp 1000.
    const fragment = new Uint8Array(12 + 64);
    fragment.set([0x20 | 0x01 | 0x02, 0, 0, 1]);
    new DataView(fragment.buffer).setUint32(4, 7, true);
    new DataView(fragment.buffer).setUint32(8, 1000, true);
    for (let i = 12; i < fragment.length; i++) fragment[i] = (i * 37) & 0xff;
    const contains = (haystack, needle) => haystack.some((_, i) => needle.every((x, j) => haystack[i + j] === x));
    for (const sessions of [setupCompact('call', 1, ['alice', 'bob']), setup('call', 1, ['alice', 'bob'])]) {
        try {
            const [alice, bob] = sessions;
            const packet = alice.encrypt(fragment, 'video', 3, 90);
            assert.ok(!contains(packet, [...fragment.subarray(0, 12)]), 'the header, orientation included, is not on the wire');
            // Turning the picture means changing byte 3 of the plaintext (90° to 270°: 1 to 3). Whatever byte of the frame a
            // relay changes to try it, including the ciphertext byte over it in a counter-mode cipher, the tag fails.
            for (let i = 0; i < packet.length; i++) {
                const tampered = packet.slice(); tampered[i] ^= 0x02;
                assert.throws(() => bob.decrypt('alice', tampered, 'video', 3, 90), `byte ${i}`);
            }
            const clear = bob.decrypt('alice', packet, 'video', 3, 90);
            assert.deepEqual(clear, fragment);
            assert.equal(clear[3], 1);
        } finally { close(sessions); }
    }
});
