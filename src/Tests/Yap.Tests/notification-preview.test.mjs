import { test } from 'node:test';
import assert from 'node:assert/strict';

// The resolver is worker code: it reads `self`, and Node has no `self` of its own.
globalThis.self ??= globalThis;
// A real import of the shipped module graph - notification-settings.js, encryption.mjs and the
// OpenPGP bundle behind it - so a graph the module service worker could not resolve fails here.
const { createPreview } = await import('../../Presentation/XFramework.Yap.Client/wwwroot/notification-preview.mjs');

const tenant = 'a'.repeat(32), me = 'b'.repeat(32), other = 'd'.repeat(32);
const account = `${tenant}:${me}`;
const thread = '5f2b8f3c-0000-4000-8000-000000000002';
const sender = '5f2b8f3c-0000-4000-8000-0000000000aa';
const device = '5f2b8f3c-0000-4000-8000-0000000000bb';
const envelope = '-----BEGIN PGP MESSAGE-----ciphertext-----END PGP MESSAGE-----';
const payload = { version: 1, kind: 'message', threadId: thread, account };

const lastMessage = (over = {}) => ({
    id: '5f2b8f3c-0000-4000-8000-0000000000cc', threadId: thread, senderId: sender,
    encryptedEnvelope: envelope, encryptionPending: false, parentId: null, isThreadReply: false,
    acceptedSenderDirectoryRevision: 3, encryptionSenderDeviceId: device, ...over
});
const conversation = (over = {}) => ({ id: thread, name: 'Ada Lovelace', group: false, lastMessage: lastMessage(), ...over });

function harness({ status = { enrolled: true, approved: true }, content = { text: 'Dinner at eight?', attachments: [] },
    items = [conversation()], settings = true, respond = null, decrypt = null } = {}) {
    const requests = [], decrypted = [];
    const api = createPreview({
        settings: { preview: async () => settings },
        e2ee: {
            status: async scope => { requests.push({ op: 'status', scope }); return status; },
            decrypt: async (scope, context, armored, directory) => {
                decrypted.push({ scope, context, armored, directory });
                if (decrypt) return decrypt(context, armored);
                return content;
            }
        },
        request: async (path, options) => {
            requests.push({ path, account: options.headers['X-Yap-Account'], credentials: options.credentials, method: options.method });
            if (respond) return respond(path);
            return { ok: true, json: async () => path === '/api/chat/conversations' ? { items, totalCount: items.length } : { credentialId: sender } };
        }
    });
    return { api, requests, decrypted };
}

test('a decrypted message becomes the sender and their words', async () => {
    const { api, requests, decrypted } = harness();
    assert.deepEqual(await api(payload), { title: 'Ada Lovelace', body: 'Dinner at eight?' });
    // Cookie-authenticated GETs only; the worker can read and can never act.
    const fetches = requests.filter(x => x.path);
    assert.deepEqual(fetches.map(x => x.path), ['/api/chat/conversations', `/api/chat/encryption/people/${sender}?senderDeviceId=${device}`]);
    assert.ok(fetches.every(x => x.credentials === 'same-origin' && x.method === undefined));
    // Byte for byte the context ChatEncryption.Context builds, or the envelope will not open.
    assert.deepEqual(decrypted[0].context, {
        tenantId: tenant, threadId: thread, messageId: lastMessage().id, senderId: sender,
        parentId: null, isThreadReply: false, kind: 'message',
        expectedSenderDeviceId: device, expectedSenderDirectoryRevision: 3
    });
});

test('the setting off keeps the banner generic and asks the server nothing', async () => {
    const { api, requests } = harness({ settings: false });
    assert.equal(await api(payload), null);
    assert.deepEqual(requests, [], 'a switched-off preview must not even reveal that this device woke up');
});

test('a device with no encryption identity gives up before reading any mail', async () => {
    for (const status of [{ enrolled: false, approved: false }, { enrolled: true, approved: false }, null]) {
        const { api, requests } = harness({ status });
        assert.equal(await api(payload), null);
        assert.deepEqual(requests.filter(x => x.path), []);
    }
});

test('an envelope this device cannot open never renders ciphertext', async () => {
    const { api } = harness({ decrypt: () => { throw new Error('This account is not an encrypted recipient.'); } });
    await assert.rejects(api(payload));
    // And a decrypt that "succeeds" with the raw armor still has to look like a message.
    const nonsense = harness({ content: { text: envelope, attachments: [] } });
    const shown = await nonsense.api(payload);
    assert.equal(shown.body, envelope, 'the resolver renders what the verified plaintext says, nothing else');
});

test('being offline or refused leaves the generic banner alone', async () => {
    const offline = harness({ respond: () => { throw new TypeError('Failed to fetch'); } });
    await assert.rejects(offline.api(payload));
    const refused = harness({ respond: () => ({ ok: false, status: 401, json: async () => ({}) }) });
    await assert.rejects(refused.api(payload));
});

// One device, two enrolled accounts. The payload says which, and nothing else can: the key store,
// the account header and the conversation all have to agree or the wrong mailbox is read.
test('multi-account routing follows the payload and never the other account', async () => {
    const { api, requests, decrypted } = harness();
    await api({ ...payload, account: `${tenant}:${other}` });
    assert.ok(requests.every(x => (x.account ?? x.scope) === `${tenant}:${other}`));
    assert.equal(decrypted[0].scope, `${tenant}:${other}`);
});

test('a push with no usable routing identifiers does nothing at all', async () => {
    for (const bad of [{}, { account }, { account: 'nonsense', threadId: thread }, { account, threadId: 'nonsense' },
        { account: `${tenant}:not-hex`, threadId: thread }]) {
        const { api, requests } = harness();
        assert.equal(await api(bad), null);
        assert.deepEqual(requests, []);
    }
});

test('a conversation that is absent, pending or unencrypted stays generic', async () => {
    for (const items of [[], [conversation({ id: '5f2b8f3c-0000-4000-8000-0000000000ff' })],
        [conversation({ lastMessage: null })],
        [conversation({ lastMessage: lastMessage({ encryptedEnvelope: null }) })],
        [conversation({ lastMessage: lastMessage({ encryptionPending: true }) })],
        [conversation({ lastMessage: lastMessage({ encryptionSenderDeviceId: null }) })],
        [conversation({ lastMessage: lastMessage({ acceptedSenderDirectoryRevision: null }) })]]) {
        const { api, decrypted } = harness({ items });
        assert.equal(await api(payload), null);
        assert.deepEqual(decrypted, []);
    }
});

// The push was for their message; by the time this runs the newest message can be one this person
// sent from another device, and their own words under a contact's name would be worse than generic.
test('a message this person sent themselves is never announced back to them', async () => {
    const mine = `${me.slice(0, 8)}-${me.slice(8, 12)}-${me.slice(12, 16)}-${me.slice(16, 20)}-${me.slice(20)}`;
    const { api, decrypted } = harness({ items: [conversation({ lastMessage: lastMessage({ senderId: mine }) })] });
    assert.equal(await api(payload), null);
    assert.deepEqual(decrypted, []);
});

test('long text is trimmed and a wordless message describes its attachments', async () => {
    const long = harness({ content: { text: `  x${'y'.repeat(400)}  `, attachments: [] } });
    const trimmed = await long.api(payload);
    assert.equal(trimmed.body.length, 140);
    assert.ok(trimmed.body.endsWith('…'));

    for (const [attachments, body] of [[1, 'Sent an attachment'], [3, 'Sent 3 attachments']]) {
        const { api } = harness({ content: { text: '   ', attachments: Array(attachments).fill({}) } });
        assert.equal((await api(payload)).body, body);
    }
    const empty = harness({ content: { text: '', attachments: [] } });
    assert.equal(await empty.api(payload), null);
});

test('a conversation with no name still names something', async () => {
    const { api } = harness({ items: [conversation({ name: '   ' })] });
    assert.equal((await api(payload)).title, 'New message');
});
