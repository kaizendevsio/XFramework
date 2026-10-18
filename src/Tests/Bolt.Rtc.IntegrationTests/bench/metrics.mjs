// Both paths use a fixed threshold, independent of their achieved FPS. Count only time above the threshold,
// including startup and trailing stalls inside the measured window. A completely stalled path stays measurable.
export const FREEZE_THRESHOLD_MS = 250;
export function freezeSummary(frames, start, end) {
    let freezes = 0, frozenMs = 0, previous = start, longestGapMs = 0;
    for (const at of [...frames, end]) {
        const gap = Math.max(0, Math.min(end, at) - previous);
        longestGapMs = Math.max(longestGapMs, gap);
        if (gap > FREEZE_THRESHOLD_MS) { freezes++; frozenMs += gap - FREEZE_THRESHOLD_MS; }
        previous = Math.min(end, at);
    }
    return { freezes, frozenSeconds: +(frozenMs / 1000).toFixed(3), longestGapMs: Math.round(longestGapMs),
        freezeThresholdMs: FREEZE_THRESHOLD_MS, freezeDefinition: 'gap excess above fixed threshold; window boundaries included' };
}
export function delaySummary(delays) {
    const sorted = [...delays].sort((a, b) => a - b);
    const q = p => sorted.length ? Math.round(sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * p))]) : null;
    return { delayP50: q(0.5), delayP99: q(0.99), delaySamples: sorted.length };
}
// Same clock domain: both peers share this benchmark page. Measure from sender encoded output to receiver encoded
// arrival, before decoding. This excludes capture/encoding and audible playout and includes encryption and transport.
export class AudioMetrics {
    constructor() { this.sent = new Map(); this.delays = []; this.sentCount = 0; this.receivedCount = 0; this.measuring = false; }
    onSent(id, at) {
        this.sent.set(id, { at, measured: this.measuring, received: false });
        if (this.measuring) this.sentCount++;
        // Warmup entries may be evicted. Keep the complete measured cohort for the fixed drain period.
        if (!this.measuring && this.sent.size > 4000) this.sent.delete(this.sent.keys().next().value);
    }
    onReceived(id, at) {
        const sent = this.sent.get(id);
        if (!sent || sent.received || !sent.measured) return;
        sent.received = true; this.receivedCount++; this.delays.push(Math.max(0, at - sent.at));
    }
    summary() {
        return { ...delaySummary(this.delays), packetsSent: this.sentCount, packetsReceived: this.receivedCount,
            deliveredPercent: this.sentCount ? +(100 * this.receivedCount / this.sentCount).toFixed(1) : null,
            definition: 'sender encoded output to receiver encoded arrival; measured send cohort plus drain; no playout' };
    }
}
