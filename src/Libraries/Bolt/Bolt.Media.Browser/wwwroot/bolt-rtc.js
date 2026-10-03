// Datagram media path for Bolt calls: one RTCPeerConnection with one data channel (unordered, never
// retransmitted, binary) to the relay, through TURN. The .NET side decides what goes on it; this module
// only drives the browser's WebRTC objects and reports back.
//
// The channel is opened in band (DCEP) rather than pre-negotiated: the relay's WebRTC stack applies a
// channel's ordering and retransmission settings only to channels opened that way.
//
// Frames arrive here already SFrame-encrypted end to end. DTLS on the channel is a hop layer only.
// Nothing here logs SDP, candidates or ICE credentials.

const channelOptions = { ordered: false, maxRetransmits: 0 };
const pathPollMs = 2000;

export function isSupported() {
    return typeof globalThis.RTCPeerConnection === 'function';
}

// Port 53 TURN URLs are blocked by browsers and only delay gathering; everything else is kept.
export function usableIceServers(servers) {
    return (servers || []).map(server => ({
        urls: (Array.isArray(server.urls) ? server.urls : [server.urls]).filter(url => typeof url === 'string' && !/:53(\?|$)/.test(url)),
        ...(server.username ? { username: server.username } : {}),
        ...(server.credential ? { credential: server.credential } : {}),
    })).filter(server => server.urls.length > 0);
}

// Read the selected candidate pair: candidate types on both sides, this side's protocol, and for a
// relay candidate the protocol to the TURN server (udp, tcp or tls).
export function selectedPath(report) {
    let pair;
    const transport = [...report.values()].find(stat => stat.type === 'transport' && stat.selectedCandidatePairId);
    if (transport) pair = report.get(transport.selectedCandidatePairId);
    pair ??= [...report.values()].find(stat => stat.type === 'candidate-pair' && stat.state === 'succeeded' && (stat.nominated || stat.selected));
    if (!pair) return null;
    const local = report.get(pair.localCandidateId);
    const remote = report.get(pair.remoteCandidateId);
    if (!local || !remote) return null;
    return {
        local: local.candidateType || 'unknown',
        localProtocol: local.protocol || 'udp',
        relayProtocol: local.relayProtocol || null,
        remote: remote.candidateType || 'unknown',
        rttMs: typeof pair.currentRoundTripTime === 'number' ? pair.currentRoundTripTime * 1000 : 0,
    };
}

export function createPeer(dotnet, options) {
    return new BoltRtcPeer(dotnet, options);
}

export class BoltRtcPeer {
    #dotnet; #pc; #channel; #maxMessage; #state = 'connecting'; #pathTimer; #closed = false; #lastPath = '';
    #dropped = 0;
    // Candidates that arrived while an offer waits for its answer: applied once the answer is, in order.
    #early = null;

    constructor(dotnet, options) {
        this.#dotnet = dotnet;
        this.#maxMessage = options?.maxMessageBytes || 1150;
        this.#pc = new RTCPeerConnection({
            iceServers: usableIceServers(options?.iceServers),
            iceTransportPolicy: options?.iceTransportPolicy === 'relay' ? 'relay' : 'all',
            bundlePolicy: 'max-bundle',
        });
        const channel = this.#channel = this.#pc.createDataChannel('bolt-media', channelOptions);
        // Safari defaults to Blob; media must arrive as bytes.
        channel.binaryType = 'arraybuffer';
        channel.bufferedAmountLowThreshold = 16 * 1024;
        channel.onopen = () => this.#setState('open');
        channel.onclose = () => this.#setState(this.#state === 'open' || this.#state === 'stalled' ? 'closed' : 'failed');
        channel.onerror = () => { /* onclose follows */ };
        channel.onbufferedamountlow = () => this.#notify('OnBufferedLow');
        channel.onmessage = event => {
            if (!(event.data instanceof ArrayBuffer) || event.data.byteLength === 0 || event.data.byteLength > this.#maxMessage) return;
            this.#notify('OnMessage', new Uint8Array(event.data));
        };
        this.#pc.onicecandidate = event => {
            const candidate = event.candidate;
            this.#notify('OnCandidate', candidate?.candidate || '', candidate?.sdpMid ?? null, candidate?.sdpMLineIndex ?? null);
        };
        this.#pc.onconnectionstatechange = () => {
            const state = this.#pc.connectionState;
            if (state === 'failed') this.#setState('failed');
            else if (state === 'closed') this.#setState('closed');
        };
        // The channel stays "open" while ICE hears nothing (Chrome declares ICE failed only ~30 s later) and while
        // it checks again after a restart. Media sent then is lost, so the channel is "stalled" and takes nothing:
        // the call's media goes on the WebSocket until ICE is connected again.
        this.#pc.oniceconnectionstatechange = () => {
            const ice = this.#pc.iceConnectionState;
            if (ice === 'failed') this.#setState('failed');
            else if (this.#state === 'open' && (ice === 'disconnected' || ice === 'checking')) this.#setState('stalled');
            else if (this.#state === 'stalled' && (ice === 'connected' || ice === 'completed')) this.#setState('open');
        };
    }

    async createOffer(iceRestart) {
        // From here until the answer is applied, the other side's candidates belong to an answer not yet here: a
        // browser would reject them (no remote description) or, on an ICE restart, file them under the old one.
        this.#early ??= [];
        const offer = await this.#pc.createOffer(iceRestart ? { iceRestart: true } : undefined);
        await this.#pc.setLocalDescription(offer);
        return this.#pc.localDescription.sdp;
    }

    async setAnswer(sdp) {
        await this.#pc.setRemoteDescription({ type: 'answer', sdp });
        const early = this.#early ?? [];
        this.#early = null;
        for (const candidate of early) await this.#apply(candidate);
    }

    async addCandidate(candidate, sdpMid, sdpMLineIndex) {
        if (this.#closed) return;
        // An empty candidate is the end of the relay's candidates.
        const init = candidate ? { candidate, sdpMid, sdpMLineIndex } : null;
        if (this.#early) { this.#early.push(init); return; }
        await this.#apply(init);
    }

    async #apply(init) {
        if (this.#closed) return;
        try { await this.#pc.addIceCandidate(init); }
        catch { /* A candidate the browser cannot use is not a failure of the path. */ }
    }

    // Synchronous on purpose: called from the sender's pacer for every frame. Never throws.
    send(bytes) {
        const channel = this.#channel;
        if (this.#closed || this.#state !== 'open' || channel.readyState !== 'open' || !bytes || bytes.length === 0 || bytes.length > this.#maxMessage) {
            this.#dropped++;
            return false;
        }
        try { channel.send(bytes); return true; }
        catch { this.#dropped++; return false; }
    }

    bufferedAmount() { return this.#closed ? 0 : this.#channel.bufferedAmount || 0; }
    dropped() { return this.#dropped; }
    state() { return this.#state; }

    close() {
        if (this.#closed) return;
        this.#closed = true;
        clearInterval(this.#pathTimer);
        try { this.#channel.close(); } catch { /* already closed */ }
        try { this.#pc.close(); } catch { /* already closed */ }
        this.#dotnet = null;
    }

    #setState(state) {
        if (this.#state === state || this.#state === 'closed' || this.#state === 'failed') return;
        this.#state = state;
        clearInterval(this.#pathTimer);
        if (state === 'open') {
            this.#pollPath();
            this.#pathTimer = setInterval(() => this.#pollPath(), pathPollMs);
        }
        this.#notify('OnState', state);
    }

    async #pollPath() {
        if (this.#closed) return;
        try {
            const path = selectedPath(await this.#pc.getStats());
            if (!path) return;
            const key = `${path.local}/${path.localProtocol}/${path.relayProtocol}/${path.remote}/${Math.round(path.rttMs / 20)}`;
            if (key === this.#lastPath) return;
            this.#lastPath = key;
            this.#notify('OnPath', path.local, path.localProtocol, path.relayProtocol, path.remote, path.rttMs);
        } catch { /* Stats are diagnostics only. */ }
    }

    #notify(method, ...args) {
        const dotnet = this.#dotnet;
        if (!dotnet) return;
        dotnet.invokeMethodAsync(method, ...args).catch(() => { /* The page or the call is going away. */ });
    }
}
