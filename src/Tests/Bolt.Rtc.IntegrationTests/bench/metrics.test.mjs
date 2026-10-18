import assert from 'node:assert/strict';
import { test } from 'node:test';
import { freezeSummary, AudioMetrics } from './metrics.mjs';
test('freeze duration includes initial, interior and trailing stalls at the same fixed threshold', () => {
    assert.deepEqual(freezeSummary([400, 500, 1000], 0, 1600), {
        freezes: 3, frozenSeconds: 0.75, longestGapMs: 600, freezeThresholdMs: 250,
        freezeDefinition: 'gap excess above fixed threshold; window boundaries included'
    });
    assert.equal(freezeSummary([], 0, 1000).frozenSeconds, 0.75);
    assert.equal(freezeSummary([250, 500, 750], 0, 1000).freezes, 0);
});
test('a slow path cannot increase the freeze threshold using its achieved frame rate', () => {
    assert.equal(freezeSummary([0, 500, 1000], 0, 1500).freezes, 3);
});
test('audio delivery uses sender cohort, deduplicates and includes late arrivals in drain', () => {
    const m = new AudioMetrics();
    m.onSent(0, 0); m.measuring = true;
    m.onSent(1, 100); m.onSent(2, 200); m.onReceived(0, 210); m.onReceived(1, 400);
    m.measuring = false; m.onSent(3, 500); m.onReceived(1, 600); m.onReceived(2, 800); m.onReceived(3, 900);
    assert.deepEqual(m.summary(), { delayP50: 600, delayP99: 600, delaySamples: 2,
        packetsSent: 2, packetsReceived: 2, deliveredPercent: 100,
        definition: 'sender encoded output to receiver encoded arrival; measured send cohort plus drain; no playout' });
});
