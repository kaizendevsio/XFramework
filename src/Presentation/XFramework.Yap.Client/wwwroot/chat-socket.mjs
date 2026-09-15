import { BoltBrowserClient } from './vendor/bolt/bolt-client.js';
import { fnv1aHash } from './vendor/bolt/protocol.js';

// One account-bound connection, independent of the currently selected conversation.
// The durable C# outbox owns retries of writes whose acceptance is uncertain.
export function createChatSocket(getListener, environment = {}) {
    const Client = environment.Client ?? BoltBrowserClient;
    const request = environment.fetch ?? globalThis.fetch.bind(globalThis);
    const network = environment.navigator ?? navigator;
    const page = environment.document ?? document;
    const origin = environment.location ?? location;
    const later = environment.setTimeout ?? setTimeout;
    const cancel = environment.clearTimeout ?? clearTimeout;
    const timeout = environment.timeout ?? (ms => AbortSignal.timeout(ms));
    const encode = new TextEncoder(), decode = new TextDecoder('utf-8', { fatal: true });
    const pushHash = fnv1aHash('yap.chat.event');
    let scope = '', thread = null, token = '', current, generation = 0, timer, delay = 1000;
    let connecting = false, stopped = false, admissionAbort;
    const seen = new Set();
    const notify = (method, ...args) => getListener()?.invokeMethodAsync(method, ...args);
    const eligible = () => scope && token && network.onLine && !page.hidden && !stopped;

    function close() {
        const old = current; current = undefined;
        old?.client.disconnect();
    }
    function retry() {
        if (timer || !scope || stopped) return;
        timer = later(() => { timer = undefined; if (eligible()) void connect(); }, delay);
        delay = Math.min(30000, delay * 2);
    }
    function failed(connection) {
        if (connection !== current) return;
        close(); retry();
    }
    async function invoke(connection, operation, body, ms = 10000) {
        if (connection !== current || connection.pending >= 32) throw new Error('Chat connection is busy or unavailable.');
        connection.pending++;
        try {
            const result = await connection.client.invoke('yap', 'yap.chat',
                encode.encode(JSON.stringify({ operation, body })), timeout(ms));
            if (connection !== current) throw new Error('The chat connection changed.');
            if (result.statusCode < 200 || result.statusCode >= 300) return { status: result.statusCode, body: null };
            return JSON.parse(decode.decode(result.payload));
        } finally { connection.pending--; }
    }
    async function watch(connection) {
        const result = await invoke(connection, 'watch', { threadId: thread || null }, 5000);
        if (result.status !== 200 && result.status !== 204) throw new Error('Chat subscription unavailable.');
    }
    async function acknowledge(connection) {
        if (connection.acking) return;
        connection.acking = true;
        try {
            while (connection === current && connection.sequence > connection.acknowledged) {
                // ACK round trips must not hold up application of the next message.
                const watermark = connection.sequence;
                const result = await invoke(connection, 'ack', { sequence: watermark });
                if (result.status !== 200 && result.status !== 204) throw new Error('Chat acknowledgment unavailable.');
                connection.acknowledged = watermark;
            }
        } catch { failed(connection); }
        finally { connection.acking = false; }
    }
    async function drain(connection) {
        if (connection.draining) return;
        connection.draining = true;
        try {
            while (connection === current && connection.queue.length) {
                const item = connection.queue.shift();
                connection.bytes -= item.bytes;
                const event = item.event;
                if (event.sequence !== connection.sequence + 1) throw new Error('Chat event gap.');
                // Transient refresh/typing/call events are not deduplicated across sockets.
                const id = event.eventId;
                if (!id || !seen.has(id)) {
                    const listener = getListener();
                    if (!listener) throw new Error('Chat listener unavailable.');
                    await listener.invokeMethodAsync('ChatSocketEvent', scope, item.json);
                    if (connection !== current) return;
                    if (id) { seen.add(id); if (seen.size > 512) seen.delete(seen.values().next().value); }
                }
                connection.sequence = event.sequence;
            }
            // Cumulative ACK follows verification/persistence, on its own lane.
            if (connection === current) void acknowledge(connection);
        } catch { failed(connection); }
        finally {
            connection.draining = false;
            if (connection === current && connection.queue.length) void drain(connection);
        }
    }
    async function connect() {
        if (!eligible() || connecting || current) return;
        connecting = true;
        const version = generation, owner = scope;
        const abort = admissionAbort = new AbortController();
        let connection;
        try {
            const response = await request('/api/chat/socket/session', {
                method: 'POST', credentials: 'same-origin', cache: 'no-store',
                signal: AbortSignal.any([abort.signal, timeout(10000)].filter(Boolean)),
                headers: { 'X-Yap-Account': owner, 'RequestVerificationToken': token }
            });
            if (version !== generation) return;
            if (response.status === 401 || response.status === 403) {
                stopped = true;
                void notify('RefreshHint')?.catch(() => {});
                return;
            }
            if (!response.ok) throw new Error('Chat admission unavailable.');
            const admission = await response.json();
            if (version !== generation) return;
            const url = new URL(admission.url, origin.href);
            if (url.protocol === 'https:') url.protocol = 'wss:';
            if (url.protocol === 'http:') url.protocol = 'ws:';
            if (url.host !== origin.host || (origin.protocol === 'https:' ? url.protocol !== 'wss:' : url.protocol !== 'ws:'))
                throw new Error('Chat socket origin mismatch.');
            const client = new Client(url.href, admission.clientId, 'yap-browser');
            connection = { client, queue: [], bytes: 0, sequence: 0, acknowledged: 0, pending: 0, draining: false, acking: false };
            current = connection;
            client.onDisconnected = () => failed(connection);
            client.onPush = (command, payload) => {
                if (connection !== current || version !== generation || command !== pushHash) return;
                try {
                    const json = decode.decode(payload), event = JSON.parse(json);
                    if (!Number.isSafeInteger(event.sequence) || event.sequence < 1 || connection.queue.length >= 128
                        || connection.bytes + payload.byteLength > 8 * 1024 * 1024) throw new Error('Chat event queue requires recovery.');
                    connection.bytes += payload.byteLength;
                    connection.queue.push({ event, json, bytes: payload.byteLength });
                    void drain(connection);
                } catch { failed(connection); }
            };
            await client.connect();
            if (connection !== current || version !== generation) { client.disconnect(); return; }
            await watch(connection);
            delay = 1000;
        } catch {
            if (version === generation) { if (connection) failed(connection); else retry(); }
        } finally {
            if (admissionAbort === abort) admissionAbort = undefined;
            connecting = false;
            if (version !== generation && eligible()) void connect();
        }
    }
    return {
        watch(account, activeThread, antiforgeryToken) {
            const changed = scope !== account;
            const changedThread = thread !== (activeThread || null);
            const changedToken = token !== (antiforgeryToken || '');
            if (changed) { generation++; admissionAbort?.abort(); close(); seen.clear(); }
            scope = account || ''; thread = activeThread || null; token = antiforgeryToken || '';
            if (changed || changedToken) { stopped = false; delay = 1000; cancel(timer); timer = undefined; }
            if (!scope) return;
            if (current?.client.isConnected && changedThread) {
                const connection = current;
                void watch(connection).catch(() => failed(connection));
            }
            else if (!current) void connect();
        },
        async request(account, operation, body) {
            const connection = current;
            if (scope !== account || !connection?.client.isConnected) return null;
            try { return await invoke(connection, operation, body); }
            catch (error) { failed(connection); throw error; }
        },
        resume() {
            if (!eligible()) return;
            cancel(timer); timer = undefined;
            if (current?.client.isConnected) {
                const connection = current;
                void watch(connection).catch(() => failed(connection));
            } else void connect();
        }
    };
}
