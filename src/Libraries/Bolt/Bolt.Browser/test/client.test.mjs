import assert from 'node:assert/strict';
import test from 'node:test';

import { BoltBrowserClient } from '../dist/bolt-client.js';
import {
    FRAME,
    MAX_FRAME_SIZE,
    readBatch,
    readFrameType,
    readRequest,
    readRequestCancel,
    writeBatch,
    writeResponse,
} from '../dist/protocol.js';

class FakeWebSocket {
    static OPEN = 1;
    static instance;

    binaryType = 'blob';
    readyState = FakeWebSocket.OPEN;
    sent = [];
    closeCode;
    onopen;
    onmessage;
    onerror;
    onclose;

    constructor() {
        FakeWebSocket.instance = this;
    }

    send(data) {
        this.sent.push(new Uint8Array(data));
    }

    close(code) {
        this.closeCode = code;
        this.readyState = 3;
    }

    emitOpen() {
        this.onopen?.({});
    }

    emitMessage(data) {
        const copy = Uint8Array.from(data);
        this.onmessage?.({ data: copy.buffer });
    }

    emitClose() {
        this.readyState = 3;
        this.onclose?.({});
    }
}

globalThis.WebSocket = FakeWebSocket;

async function connectClient() {
    const client = new BoltBrowserClient('ws://localhost/bolt', 'browser', 'Browser');
    const connecting = client.connect();
    const socket = FakeWebSocket.instance;
    socket.emitOpen();
    socket.emitMessage(new Uint8Array([FRAME.RegisterAck, 1, 2, 0]));
    await connecting;
    return { client, socket };
}

test('caller abort rejects the RPC and sends RequestCancel', async () => {
    const { client, socket } = await connectClient();
    const controller = new AbortController();
    const invocation = client.invoke('service', 'command', new Uint8Array([1]), controller.signal);

    controller.abort();

    await assert.rejects(invocation, error => error?.name === 'AbortError');
    await Promise.resolve();
    const sentFrames = socket.sent.flatMap(frame => readFrameType(frame) === FRAME.Batch ? readBatch(frame) : [frame]);
    const cancel = sentFrames.find(frame => readFrameType(frame) === FRAME.RequestCancel);
    assert.ok(cancel);
    assert.ok(readRequestCancel(cancel));
    client.disconnect();
});

test('registration fails clearly when the wire version does not match', async () => {
    const client = new BoltBrowserClient('ws://localhost/bolt', 'browser', 'Browser');
    const connecting = client.connect();
    const socket = FakeWebSocket.instance;
    socket.emitOpen();
    socket.emitMessage(new Uint8Array([FRAME.RegisterAck, 1, 1, 0]));

    await assert.rejects(connecting, /wire version mismatch.*expected 2.*received 1/i);
    assert.equal(socket.closeCode, 1002);
    client.disconnect();
});

test('non-media frames batch within one microtask while a single frame stays unbatched', async () => {
    const { client, socket } = await connectClient();
    client.push('service', 'one', new Uint8Array([1]));
    await Promise.resolve();
    assert.equal(readFrameType(socket.sent.at(-1)), FRAME.Push);

    client.push('service', 'one', new Uint8Array([1]));
    client.push('service', 'two', new Uint8Array([2]));
    await Promise.resolve();
    const batch = socket.sent.at(-1);
    assert.equal(readFrameType(batch), FRAME.Batch);
    assert.equal(readBatch(batch).length, 2);
    client.disconnect();
});

test('push rejects empty and whitespace recipients', async () => {
    const { client } = await connectClient();

    assert.throws(() => client.push('', 'one', new Uint8Array([1])), /nonblank recipientId/i);
    assert.throws(() => client.push('   ', 'one', new Uint8Array([1])), /nonblank recipientId/i);

    client.disconnect();
});

test('media bypass flushes queued control frames and preserves ordering', async () => {
    const { client, socket } = await connectClient();
    client.push('service', 'one', new Uint8Array([1]));
    client.startCall('recipient');

    const frames = socket.sent.slice(1);
    assert.equal(readFrameType(frames[0]), FRAME.Push);
    assert.equal(readFrameType(frames[1]), FRAME.CallSignal);
    client.disconnect();
});

test('inbound batches dispatch each inner response', async () => {
    const { client, socket } = await connectClient();
    const first = client.invoke('service', 'one', new Uint8Array([1]));
    const second = client.invoke('service', 'two', new Uint8Array([2]));
    await Promise.resolve();
    const requests = socket.sent
        .filter(frame => readFrameType(frame) === FRAME.Batch)
        .flatMap(frame => readBatch(frame))
        .map(frame => readRequest(frame));
    assert.equal(requests.length, 2);

    socket.emitMessage(writeBatch(requests.map(request => writeResponse(request.requestId, 200, new Uint8Array([9])))));
    assert.deepEqual((await first).payload, new Uint8Array([9]));
    assert.deepEqual((await second).payload, new Uint8Array([9]));
    client.disconnect();
});

test('unexpected close rejects pending RPC work', async () => {
    const { client, socket } = await connectClient();
    const invocation = client.invoke('service', 'command', new Uint8Array([1]));

    socket.emitClose();

    await assert.rejects(invocation, /connection closed/i);
    client.disconnect();
});

test('oversized inbound frame closes the socket with message-too-big', async () => {
    const { client, socket } = await connectClient();

    socket.emitMessage(new Uint8Array(MAX_FRAME_SIZE + 1));

    assert.equal(socket.closeCode, 1009);
    client.disconnect();
});

test('registration timeout closes and rejects a connection without an acknowledgement', async () => {
    const client = new BoltBrowserClient('ws://localhost/bolt', 'browser', 'Browser');
    client.registrationTimeoutMs = 5;
    const connecting = client.connect();
    const socket = FakeWebSocket.instance;
    socket.emitOpen();

    await assert.rejects(connecting, /registration timed out/i);
    assert.equal(socket.closeCode, 1002);
    client.disconnect();
});

test('malformed in-range frame rejects active work before the close callback', async () => {
    const { client, socket } = await connectClient();
    const invocation = client.invoke('service', 'command', new Uint8Array([1]));

    socket.emitMessage(new Uint8Array([FRAME.Response]));

    await assert.rejects(invocation, /malformed Bolt frame/i);
    assert.equal(socket.closeCode, 1002);
    client.disconnect();
});
