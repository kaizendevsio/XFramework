import assert from 'node:assert/strict';
import { test } from 'node:test';
import { createPeer, isSupported, selectedPath, usableIceServers } from '../wwwroot/bolt-rtc.js';

// A stand-in for the browser's RTCPeerConnection: records what the module asks for and lets a test
// drive the events a real one would raise.
class FakeChannel {
    readyState = 'connecting';
    bufferedAmount = 0;
    sent = [];
    throwOnSend = false;
    constructor(label, init) { this.label = label; this.init = init; }
    send(data) { if (this.throwOnSend) throw new Error('closing'); this.sent.push(data); }
    close() { this.readyState = 'closed'; }
}
class FakePeerConnection {
    static last;
    connectionState = 'new';
    iceConnectionState = 'new';
    localDescription = null;
    candidates = [];
    offers = [];
    constructor(config) { this.config = config; FakePeerConnection.last = this; }
    createDataChannel(label, init) { return this.channel = new FakeChannel(label, init); }
    async createOffer(options) { this.offers.push(options); return { type: 'offer', sdp: 'v=0 offer' }; }
    async setLocalDescription(description) { this.localDescription = description; }
    async setRemoteDescription(description) { this.remote = description; }
    async addIceCandidate(candidate) { this.candidates.push(candidate); }
    async getStats() { return this.stats ?? new Map(); }
    close() { this.closed = true; }
}
globalThis.RTCPeerConnection = FakePeerConnection;

function dotnet() {
    const calls = [];
    return { calls, invokeMethodAsync: (method, ...args) => { calls.push([method, ...args]); return Promise.resolve(); } };
}
const flush = () => new Promise(resolve => setTimeout(resolve, 0));
function open(peer) {
    const pc = FakePeerConnection.last;
    pc.channel.readyState = 'open';
    pc.channel.onopen();
    return pc;
}

test('the media channel is pre-negotiated, unordered, never retransmitted and binary', () => {
    const target = dotnet();
    const peer = createPeer(target, { iceServers: [], iceTransportPolicy: 'all', maxMessageBytes: 1150 });
    const pc = FakePeerConnection.last;
    assert.deepEqual(pc.channel.init, { negotiated: true, id: 0, ordered: false, maxRetransmits: 0 });
    assert.equal(pc.channel.binaryType, 'arraybuffer', 'Safari would otherwise hand over Blobs');
    assert.equal(pc.config.iceTransportPolicy, 'all');
    assert.equal(pc.config.bundlePolicy, 'max-bundle');
    assert.equal(isSupported(), true);
    peer.close();
});

test('ICE servers keep their credentials and lose the port browsers block', () => {
    const servers = usableIceServers([
        { urls: ['turn:t.example:3478?transport=udp', 'turn:t.example:53?transport=udp', 'turns:t.example:5349?transport=tcp'], username: 'u', credential: 'c' },
        { urls: 'stun:t.example:53' },
    ]);
    assert.deepEqual(servers, [{ urls: ['turn:t.example:3478?transport=udp', 'turns:t.example:5349?transport=tcp'], username: 'u', credential: 'c' }]);
    createPeer(dotnet(), { iceServers: servers, iceTransportPolicy: 'relay' }).close();
    assert.equal(FakePeerConnection.last.config.iceTransportPolicy, 'relay');
});

test('offers, ICE restarts, answers and trickled candidates reach the browser objects', async () => {
    const target = dotnet();
    const peer = createPeer(target, {});
    const pc = FakePeerConnection.last;
    assert.equal(await peer.createOffer(false), 'v=0 offer');
    await peer.createOffer(true);
    assert.deepEqual(pc.offers, [undefined, { iceRestart: true }]);
    await peer.setAnswer('v=0 answer');
    assert.deepEqual(pc.remote, { type: 'answer', sdp: 'v=0 answer' });
    await peer.addCandidate('candidate:1 1 udp 1 198.51.100.7 3478 typ relay', '0', 0);
    await peer.addCandidate('', null, null);
    assert.deepEqual(pc.candidates, [{ candidate: 'candidate:1 1 udp 1 198.51.100.7 3478 typ relay', sdpMid: '0', sdpMLineIndex: 0 }, null]);
    pc.onicecandidate({ candidate: { candidate: 'candidate:2', sdpMid: '0', sdpMLineIndex: 0 } });
    pc.onicecandidate({ candidate: null });
    assert.deepEqual(target.calls.filter(x => x[0] === 'OnCandidate'), [['OnCandidate', 'candidate:2', '0', 0], ['OnCandidate', '', null, null]]);
    peer.close();
});

test('sends are synchronous, bounded and never throw', () => {
    const peer = createPeer(dotnet(), { maxMessageBytes: 1150 });
    assert.equal(peer.send(new Uint8Array(10)), false, 'not open yet');
    const pc = open(peer);
    assert.equal(peer.send(new Uint8Array(10)), true);
    assert.equal(peer.send(new Uint8Array(1151)), false, 'a message that would be split is refused');
    pc.channel.throwOnSend = true;
    assert.equal(peer.send(new Uint8Array(10)), false);
    assert.equal(peer.dropped(), 3);
    pc.channel.bufferedAmount = 4096;
    assert.equal(peer.bufferedAmount(), 4096);
    peer.close();
    assert.equal(peer.bufferedAmount(), 0);
});

test('only whole binary messages reach .NET', () => {
    const target = dotnet();
    const peer = createPeer(target, { maxMessageBytes: 100 });
    const pc = open(peer);
    pc.channel.onmessage({ data: new Uint8Array([0x21, 1, 2]).buffer });
    pc.channel.onmessage({ data: 'text' });
    pc.channel.onmessage({ data: new ArrayBuffer(101) });
    const messages = target.calls.filter(x => x[0] === 'OnMessage');
    assert.equal(messages.length, 1);
    assert.deepEqual([...messages[0][1]], [0x21, 1, 2]);
    peer.close();
});

test('states are reported once, failure is final, and a closed peer reports nothing more', async () => {
    const target = dotnet();
    const peer = createPeer(target, {});
    const pc = open(peer);
    pc.connectionState = 'failed';
    pc.onconnectionstatechange();
    pc.channel.onclose();
    peer.close();
    pc.channel.onopen();
    await flush();
    assert.deepEqual(target.calls.filter(x => x[0] === 'OnState').map(x => x[1]), ['open', 'failed']);
    assert.equal(pc.closed, true);
});

test('the selected path names both candidate types and the protocol to the TURN server', () => {
    const stats = new Map([
        ['T', { type: 'transport', selectedCandidatePairId: 'P' }],
        ['P', { type: 'candidate-pair', localCandidateId: 'L', remoteCandidateId: 'R', state: 'succeeded', currentRoundTripTime: 0.25 }],
        ['L', { type: 'local-candidate', candidateType: 'relay', protocol: 'udp', relayProtocol: 'tls' }],
        ['R', { type: 'remote-candidate', candidateType: 'relay', protocol: 'udp' }],
    ]);
    assert.deepEqual(selectedPath(stats), { local: 'relay', localProtocol: 'udp', relayProtocol: 'tls', remote: 'relay', rttMs: 250 });
    // Firefox has no transport stats; the nominated succeeded pair is used instead.
    stats.delete('T');
    stats.get('P').nominated = true;
    assert.equal(selectedPath(stats).relayProtocol, 'tls');
    assert.equal(selectedPath(new Map()), null);
});

test('an open channel reports its path', async () => {
    const target = dotnet();
    const peer = createPeer(target, {});
    FakePeerConnection.last.stats = new Map([
        ['P', { type: 'candidate-pair', localCandidateId: 'L', remoteCandidateId: 'R', state: 'succeeded', nominated: true, currentRoundTripTime: 0.1 }],
        ['L', { type: 'local-candidate', candidateType: 'srflx', protocol: 'udp' }],
        ['R', { type: 'remote-candidate', candidateType: 'relay', protocol: 'udp' }],
    ]);
    open(peer);
    await flush(); await flush();
    assert.deepEqual(target.calls.find(x => x[0] === 'OnPath'), ['OnPath', 'srflx', 'udp', null, 'relay', 100]);
    peer.close();
});
