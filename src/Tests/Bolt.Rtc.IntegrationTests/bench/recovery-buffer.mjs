// Benchmark-only port. RecoveryBufferBenchmarkTests checks decisions against the current C# implementation.
const FRAGMENT_HEADER = 12, MAX_PICTURE_BYTES = 96 * (4096 - FRAGMENT_HEADER);
// .NET Dictionary reuses removed slots; its enumeration can ask a fragment before another entry gives that
// picture up in the same Poll. Match that order so the parity check covers transient NACKs and counters too.
class SlotMap extends Map {
    positions = new Map(); slots = []; free = [];
    set(key, value) {
        if (!this.has(key)) { const i = this.free.length ? this.free.pop() : this.slots.length; this.positions.set(key, i); this.slots[i] = key; }
        return super.set(key, value);
    }
    delete(key) {
        if (!super.delete(key)) return false;
        const i = this.positions.get(key); this.positions.delete(key); this.slots[i] = undefined; this.free.push(i); return true;
    }
    clear() { super.clear(); this.positions.clear(); this.slots = []; this.free = []; }
    *entries() { for (const key of this.slots) if (key !== undefined) yield [key, this.get(key)]; }
    *keys() { for (const key of this.slots) if (key !== undefined) yield key; }
    *values() { for (const key of this.slots) if (key !== undefined) yield this.get(key); }
    [Symbol.iterator]() { return this.entries(); }
}

export class RecoveryBuffer {
    static REORDER = 15; static MAX_TRIES = 3; static MAX_WINDOW = 1500;
    constructor(recover, keyframeSupersedes = true) { this.recover = recover; this.keyframeSupersedes = keyframeSupersedes; this.window = 0; this.rtt = 200; this.pictures = recover ? new SlotMap() : new Map(); this.missing = new SlotMap();
        this.highest = null; this.lastReleased = null; this.lastHandled = null; this.localLoss = false; this.layered = false;
        this.bytes = 0; this.lastArrival = 0;
        this.topLayer = 0; this.skipAbove = null; this.stats = { nacked: 0, recovered: 0, abandoned: 0, declined: 0, skipped: 0, incomplete: 0 }; }
    configure(rttMs) { this.rtt = Math.max(1, Math.min(5000, Math.trunc(rttMs))); this.window = this.recover ? Math.max(350, Math.min(RecoveryBuffer.MAX_WINDOW, Math.trunc(this.rtt * 1.5) + 50 + 250)) : 0; }
    static newer(a, b) { const d = (a - b) >>> 0; return d > 0 && d < 0x80000000; }
    push(sequence, fragment, at, ready) {
        const version = fragment[0] & 0xf0;
        if (fragment.length <= FRAGMENT_HEADER || fragment.length > 4096 || (version !== 0x10 && version !== 0x20)) return;
        const view = new DataView(fragment.buffer, fragment.byteOffset, fragment.byteLength);
        const total = fragment[1] + 1, index = version === 0x10 ? view.getUint16(2, true) : fragment[2], frameId = view.getUint32(4, true);
        const orientation = version === 0x20 ? fragment[3] : 0;
        if (index >= total || (orientation & ~7) !== 0 || (index !== total - 1 && fragment.length - FRAGMENT_HEADER < 256)) return;
        const header = { total, index, frameId, orientation, timestamp: view.getUint32(8, true), keyframe: (fragment[0] & 1) !== 0, layer: (fragment[0] & 0x0c) >> 2 };
        const payload = fragment.subarray(FRAGMENT_HEADER);
        if (!this.recover) return this.#plain(header, payload, ready);
        this.lastArrival = at;
        const asked = this.missing.get(sequence);
        if (asked) { this.missing.delete(sequence); if (asked.tries > 0) this.stats.recovered++; }
        if (this.highest === null) this.highest = sequence;
        else if (RecoveryBuffer.newer(sequence, this.highest)) {
            const gap = (sequence - this.highest) >>> 0;
            if (gap <= 512) for (let o = 1; o < gap; o++) this.#addMissing((this.highest + o) >>> 0, at);
            else { this.missing.clear(); this.localLoss = true; }
            this.highest = sequence;
        }
        if (this.lastHandled !== null && !RecoveryBuffer.newer(frameId, this.lastHandled)) return;
        const first = (sequence - index) >>> 0;
        let slot = this.pictures.get(frameId);
        if (!slot) { if (this.pictures.size >= 48) this.#loseOldest(); slot = { frameId, first, total, orientation, parts: new Array(total), received: 0, bytes: 0, fragmentSize: 0, keyframe: false, layer: 0, lastSeen: at, complete: false, lost: false }; this.pictures.set(frameId, slot); }
        else if (slot.total !== total || slot.first !== first || slot.orientation !== orientation) { this.#lose(slot); return this.#release(ready); }
        if (slot.lost || slot.complete || slot.parts[index]) return;
        const size = index === total - 1 ? 0 : payload.length;
        if ((size && slot.fragmentSize && size !== slot.fragmentSize) || (index === total - 1 && slot.fragmentSize && payload.length > slot.fragmentSize) || slot.bytes + payload.length > MAX_PICTURE_BYTES) { this.#lose(slot); return this.#release(ready); }
        if (size) slot.fragmentSize = size;
        slot.lastSeen = at; this.bytes += payload.length;
        slot.parts[index] = payload.slice(); slot.received++; slot.bytes += payload.length; slot.timestamp = header.timestamp;
        if (header.keyframe) slot.keyframe = true;
        slot.layer = header.layer; if (header.layer > 0) this.layered = true; this.topLayer = Math.max(this.topLayer, header.layer);
        if (slot.received === slot.total) { if (slot.total > 1 && slot.parts[slot.total - 1].length > slot.fragmentSize) this.#lose(slot); else slot.complete = true; }
        if (slot.complete && slot.keyframe && this.keyframeSupersedes) {
            for (const old of [...this.pictures.values()]) if (RecoveryBuffer.newer(slot.first, old.first)) {
                const complete = old.complete;
                this.#lose(old);
                this.#remove(old);
                if (!complete) this.#giveUp(old); else this.#handled(old.frameId);
            }
            for (const missing of [...this.missing.keys()]) if (RecoveryBuffer.newer(slot.first, missing)) this.missing.delete(missing);
        }
        while (this.bytes > 2 * 1024 * 1024 && this.pictures.size) this.#loseOldest();
        this.#release(ready);
    }
    poll(at, ready, nacks) {
        if (!this.recover) return;
        if (this.highest !== null && at - this.lastArrival >= Math.max(40, Math.min(300, this.rtt * 2))) for (const picture of this.pictures.values()) {
            if (picture.complete || picture.lost) continue;
            for (let i = 0; i < picture.total; i++) {
                const s = (picture.first + i) >>> 0;
                if (!picture.parts[i] && RecoveryBuffer.newer(s, this.highest)) this.#addMissing(s, this.lastArrival);
            }
        }
        for (const [sequence, missing] of [...this.missing]) {
            const age = at - missing.since, owner = this.#owner(sequence);
            if (owner?.lost) { this.missing.delete(sequence); continue; }
            if (age >= this.window) {
                this.missing.delete(sequence); this.stats.abandoned++;
                if (owner) this.#lose(owner); else this.localLoss = true;
                continue;
            }
            const due = missing.tries === 0 || at - missing.lastAsked >= Math.trunc(this.rtt * 1.2) + 10;
            if (age >= RecoveryBuffer.REORDER && missing.tries < RecoveryBuffer.MAX_TRIES && age + this.rtt <= this.window && due) {
                nacks.push(sequence); missing.tries++; missing.lastAsked = at; this.stats.nacked++;
            }
        }
        for (const picture of this.pictures.values())
            if (!picture.complete && !picture.lost && at - picture.lastSeen >= this.window + this.rtt) this.#lose(picture);
        this.#release(ready);
    }
    decline(sequences, ready) {
        if (!this.recover) return;
        for (const sequence of sequences) {
            if (!this.missing.delete(sequence)) continue;
            this.stats.declined++;
            const owner = this.#owner(sequence);
            if (owner && !owner.complete) this.#lose(owner);
            else if (!owner && this.layered) this.skipAbove = Math.min(this.skipAbove ?? 1, 1);
        }
        this.#release(ready);
    }
    #remove(p) { this.pictures.delete(p.frameId); this.bytes -= p.bytes; }
    #giveUp(p) { this.#handled(p.frameId); this.stats.incomplete++; if (p.keyframe || p.layer === 0 || !this.layered) this.localLoss = true; else this.skipAbove = Math.min(this.skipAbove ?? p.layer, p.layer); }
    #loseOldest() { const p = this.#oldest(); if (p) { this.#lose(p); this.#remove(p); this.#giveUp(p); } }
    #addMissing(sequence, since) { if (this.missing.size < 1024 && !this.missing.has(sequence)) this.missing.set(sequence, { since, tries: 0, lastAsked: 0 }); }
    #owner(sequence) { for (const p of this.pictures.values()) if (((sequence - p.first) >>> 0) < p.total) return p; return null; }
    #oldest() { let oldest = null; for (const p of this.pictures.values()) if (!oldest || RecoveryBuffer.newer(oldest.first, p.first)) oldest = p; return oldest; }
    #lose(p) { if (p.lost) return; p.lost = true; p.complete = false; for (let i = 0; i < p.total; i++) this.missing.delete((p.first + i) >>> 0); }
    #handled(id) { if (this.lastHandled === null || RecoveryBuffer.newer(id, this.lastHandled)) this.lastHandled = id; }
    #release(ready) {
        for (let p = this.#oldest(); p; p = this.#oldest()) {
            if (p.lost) { this.#remove(p); this.#giveUp(p); continue; }
            if (!p.complete) {
                if (this.layered && p.layer > 0 && p.layer >= this.topLayer && [...this.pictures.values()].some(x => x.complete && x !== p)) { this.#lose(p); continue; }
                break;
            }
            if ([...this.missing.keys()].some(s => RecoveryBuffer.newer(p.first, s))) break;
            this.#remove(p);
            this.#emit(p, ready);
        }
    }
    #emit(p, ready) {
        this.#handled(p.frameId);
        if (this.skipAbove !== null) {
            if (!p.keyframe && p.layer > this.skipAbove) { this.stats.skipped++; return; }
            this.skipAbove = null;
        }
        const gap = this.lastReleased !== null && ((p.frameId - this.lastReleased) >>> 0) !== 1;
        const discontinuity = (this.lastReleased === null && !p.keyframe) || (gap && (!this.layered || this.localLoss));
        if (p.keyframe || discontinuity) this.localLoss = false;
        const data = new Uint8Array(p.bytes); let offset = 0;
        for (const part of p.parts) { data.set(part, offset); offset += part.length; }
        this.lastReleased = p.frameId;
        ready.push({ data, timestamp: p.timestamp, keyframe: p.keyframe, discontinuity, frameId: p.frameId, layer: p.keyframe ? 0 : p.layer, orientation: p.orientation });
    }
    // The assembler without recovery (VideoFrameAssembler): emit on completion; older partial pictures are loss.
    #plain(header, payload, ready) {
        const { frameId, total, index } = header;
        if (this.lastReleased !== null && !RecoveryBuffer.newer(frameId, this.lastReleased)) return;
        let slot = this.pictures.get(frameId);
        if (!slot) { if (this.pictures.size >= 3) { this.pictures.delete(this.pictures.keys().next().value); this.stats.incomplete++; this.localLoss = true; } slot = { frameId, parts: new Array(total), received: 0, bytes: 0, fragmentSize: 0, keyframe: false, layer: 0, total, orientation: header.orientation }; this.pictures.set(frameId, slot); }
        else if (slot.total !== total || slot.orientation !== header.orientation) { this.pictures.delete(frameId); this.stats.incomplete++; this.localLoss = true; return; }
        if (slot.parts[index]) return;
        const size = index === total - 1 ? 0 : payload.length;
        if ((size && slot.fragmentSize && size !== slot.fragmentSize) || (index === total - 1 && slot.fragmentSize && payload.length > slot.fragmentSize) || slot.bytes + payload.length > MAX_PICTURE_BYTES) { this.pictures.delete(frameId); this.stats.incomplete++; this.localLoss = true; return; }
        if (size) slot.fragmentSize = size;
        slot.parts[index] = payload.slice(); slot.received++; slot.bytes += payload.length; slot.timestamp = header.timestamp;
        if (header.keyframe) slot.keyframe = true;
        slot.layer = header.layer; if (header.layer > 0) this.layered = true;
        if (slot.received !== slot.total) return;
        if (slot.total > 1 && slot.parts[slot.total - 1].length > slot.fragmentSize) { this.pictures.delete(frameId); this.stats.incomplete++; this.localLoss = true; return; }
        this.pictures.delete(frameId);
        for (const [id, stale] of [...this.pictures]) if (RecoveryBuffer.newer(frameId, id)) { this.pictures.delete(id); this.stats.incomplete++; if (!slot.keyframe) this.localLoss = true; }
        const gap = this.lastReleased !== null && ((frameId - this.lastReleased) >>> 0) !== 1;
        const discontinuity = (this.lastReleased === null && !slot.keyframe) || (gap && (!this.layered || this.localLoss));
        if (slot.keyframe || discontinuity) this.localLoss = false;
        const data = new Uint8Array(slot.bytes); let offset = 0;
        for (const part of slot.parts) { data.set(part, offset); offset += part.length; }
        this.lastReleased = frameId;
        ready.push({ data, timestamp: slot.timestamp, keyframe: slot.keyframe, discontinuity, frameId, layer: slot.keyframe ? 0 : slot.layer, orientation: slot.orientation });
    }
}

