import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const root = new URL('../../Presentation/XFramework.Yap.Client/wwwroot/', import.meta.url);
const source = readFileSync(new URL('time.js', root), 'utf8');

// The published client has no ICU and no timezone database, so this file is the only thing standing
// between a UTC instant and the hour a person actually reads. These run it under a real zone.
function load(zone, now) {
    process.env.TZ = zone;
    const context = { window: { yap: {} }, Intl, Date: makeDate(now), console };
    vm.runInNewContext(source, context);
    return context.window.yap.time;
}

// A fixed "now", so "Today" and "Yesterday" are decided against a known clock rather than the wall.
function makeDate(now) {
    return class extends Date {
        constructor(...args) { super(...(args.length ? args : [now])); }
        static now() { return now; }
    };
}

const noon = Date.UTC(2026, 8, 17, 12, 0, 0);          // 2026-09-17T12:00Z
const lateUtc = Date.UTC(2026, 8, 17, 23, 30, 0);      // still the 17th in UTC, the 18th in Tokyo

test('the clock shows the viewer\'s zone, not UTC', () => {
    assert.equal(load('UTC', noon).clock(noon), '12:00 PM');
    assert.equal(load('Asia/Tokyo', noon).clock(noon), '9:00 PM');
    assert.equal(load('America/Los_Angeles', noon).clock(noon), '5:00 AM');
});

test('the day key is the viewer\'s calendar day, which is what groups a conversation', () => {
    assert.equal(load('UTC', lateUtc).dayKey(lateUtc), '2026-09-17');
    assert.equal(load('Asia/Tokyo', lateUtc).dayKey(lateUtc), '2026-09-18');
    assert.equal(load('America/Los_Angeles', lateUtc).dayKey(lateUtc), '2026-09-17');
});

test('"Today" and "Yesterday" are counted in calendar days, so a zone cannot shift them', () => {
    const utc = load('UTC', lateUtc);
    assert.equal(utc.daySeparator(lateUtc), 'Today');
    assert.equal(utc.daySeparator(lateUtc - 86400000), 'Yesterday');
    // 23:30Z is already 08:30 on the 18th in Tokyo, so 13:30Z the same UTC day is the 17th there -
    // "Yesterday" to this viewer and "Today" to a UTC one. That split is the whole reason the
    // browser decides this and .NET does not.
    const tokyo = load('Asia/Tokyo', lateUtc);
    assert.equal(tokyo.daySeparator(lateUtc), 'Today');
    assert.equal(tokyo.daySeparator(lateUtc - 10 * 3600000), 'Yesterday');
    assert.equal(utc.daySeparator(lateUtc - 10 * 3600000), 'Today');
});

test('an older separator falls back to a full date rather than a bare weekday', () => {
    const api = load('UTC', noon);
    assert.equal(api.daySeparator(Date.UTC(2026, 0, 2, 9, 0, 0)), 'Friday, January 2, 2026');
});

test('a search stamp tightens as a hit gets older', () => {
    const api = load('UTC', noon);
    assert.equal(api.stamp(noon - 3600000), '11:00 AM');
    assert.equal(api.stamp(noon - 86400000), 'Yesterday');
    assert.equal(api.stamp(noon - 3 * 86400000), 'Mon');
    assert.equal(api.stamp(Date.UTC(2026, 1, 3, 12, 0, 0)), 'Feb 3');
    assert.equal(api.stamp(Date.UTC(2025, 1, 3, 12, 0, 0)), 'Feb 2025');
});

test('a half-hour zone still lands on the right minute', () => {
    assert.equal(load('Asia/Kolkata', noon).clock(noon), '5:30 PM');
    assert.equal(load('Australia/Adelaide', noon).clock(noon), '9:30 PM');
});

test('a date inside daylight saving uses that day\'s offset, not a fixed one', () => {
    const july = Date.UTC(2026, 6, 1, 12, 0, 0), january = Date.UTC(2026, 0, 1, 12, 0, 0);
    const api = load('Europe/London', noon);
    assert.equal(api.clock(july), '1:00 PM');     // BST
    assert.equal(api.clock(january), '12:00 PM'); // GMT
});
