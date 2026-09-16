import { encryption } from './encryption.mjs';
import './notification-settings.js';

// Turns a routing-only push payload into "who said what", entirely on this device.
//
// It reuses encryption.mjs unchanged - the same directory validation, pinning, rollback and
// revocation checks the app performs - because a second decrypt path that missed one revocation
// check would render attacker-chosen text on a lock screen under a trusted contact's name.
//
// Nothing is stored. The plaintext exists for the length of one showNotification call and is never
// written to Cache Storage, IndexedDB or the network.
const ACCOUNT = /^[0-9a-f]{32}:[0-9a-f]{32}$/;
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const LIMIT = 140;

export function createPreview({ e2ee = encryption, settings = self.yapNotificationSettings, request = (...args) => fetch(...args) } = {}) {
    const json = async (path, account, signal) => {
        // Same-origin cookies authenticate this; the BFF only demands an antiforgery token on
        // non-GET, so a worker can read but can never act. X-Yap-Account must match the signed-in
        // cookie, which is what makes a push for a second enrolled account fail closed rather than
        // read the wrong mailbox.
        const response = await request(path, { credentials: 'same-origin', cache: 'no-store', signal, headers: { 'X-Yap-Account': account } });
        if (!response.ok) throw new Error(`Unavailable: ${response.status}`);
        return response.json();
    };
    return async function preview(payload, signal) {
        // Routing identifiers only, and every one of them is checked before it reaches a URL.
        const account = String(payload?.account ?? '').toLowerCase();
        const thread = String(payload?.threadId ?? '').toLowerCase();
        if (!ACCOUNT.test(account) || !GUID.test(thread)) return null;
        if (!await settings.preview(account)) return null;
        // An unenrolled or revoked device holds nothing that can open an envelope, and asking it to
        // try would only spend the budget. This is the "unlock or verify this device" state.
        const status = await e2ee.status(account);
        if (!status?.enrolled || !status.approved) return null;

        // The conversation list, not the message list: it already carries the last envelope and the
        // directory-resolved name in one round trip, and unlike /messages it has no delivery
        // acknowledgement to suppress - the thread list request has no such flag to begin with - so
        // reading it in the background cannot mark anything delivered behind the person's back.
        const page = await json('/api/chat/conversations', account, signal);
        const conversation = page?.items?.find(item => String(item?.id).toLowerCase() === thread);
        const message = conversation?.lastMessage;
        if (!message?.encryptedEnvelope || message.encryptionPending) return null;
        if (!GUID.test(String(message.senderId ?? '').toLowerCase()) || !GUID.test(String(message.encryptionSenderDeviceId ?? '').toLowerCase())) return null;
        if (typeof message.acceptedSenderDirectoryRevision !== 'number') return null;
        // The newest message may have arrived from this person's own other device while the push
        // was in flight; their own words under a contact's name would be worse than generic.
        if (String(message.senderId).replaceAll('-', '') === account.split(':')[1]) return null;

        const directory = await json(`/api/chat/encryption/people/${message.senderId}?senderDeviceId=${message.encryptionSenderDeviceId}`, account, signal);
        // Byte for byte the context ChatEncryption.Context builds; the envelope is bound to it, so
        // any drift here fails the decrypt rather than showing the wrong thing.
        const content = await e2ee.decrypt(account, {
            tenantId: account.split(':')[0], threadId: thread, messageId: message.id, senderId: message.senderId,
            parentId: message.parentId ?? null, isThreadReply: message.isThreadReply === true, kind: 'message',
            expectedSenderDeviceId: message.encryptionSenderDeviceId,
            expectedSenderDirectoryRevision: message.acceptedSenderDirectoryRevision
        }, message.encryptedEnvelope, directory);

        const text = typeof content?.text === 'string' ? content.text.replace(/\s+/g, ' ').trim() : '';
        const attachments = Array.isArray(content?.attachments) ? content.attachments.length : 0;
        const body = text ? (text.length > LIMIT ? `${text.slice(0, LIMIT - 1).trimEnd()}…` : text)
            : attachments ? attachments === 1 ? 'Sent an attachment' : `Sent ${attachments} attachments` : '';
        if (!body) return null;
        // In a direct message the conversation name is the person who wrote it. In a group it is the
        // group: the conversation row does not carry per-message sender names, and a second request
        // to find one would spend the budget on a line the group name already explains.
        const title = typeof conversation.name === 'string' && conversation.name.trim() ? conversation.name.trim() : 'New message';
        return { title, body };
    };
}

export const preview = createPreview();
