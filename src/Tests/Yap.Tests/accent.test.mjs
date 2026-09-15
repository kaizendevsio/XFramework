import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

function fixture(saved) {
    const tokens = new Map(), storage = new Map(saved ? [['yap-accent', saved]] : []);
    const root = { dataset: { theme: 'light' }, style: { setProperty: (key, value) => tokens.set(key, value) } };
    const window = {};
    const context = vm.createContext({ window, document: { documentElement: root, querySelector: () => ({}) },
        localStorage: { getItem: key => storage.get(key), setItem: (key, value) => storage.set(key, value) } });
    const source = readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/app.js', import.meta.url), 'utf8').split('// Focus rings')[0];
    vm.runInContext(source, context); context.yap = window.yap;
    return { api: window.yap, tokens, storage };
}

test('accent survives theme changes and selects readable text for light and dark colors', () => {
    const f = fixture();
    f.api.setAccent('#000000'); assert.equal(f.tokens.get('--on-accent'), '#ffffff');
    f.api.setAccent('#ffffff'); assert.equal(f.tokens.get('--on-accent'), '#161616');
    f.api.setAccent('#82b5ff'); f.api.applyTheme('dark');
    assert.equal(f.tokens.get('--accent'), '#82b5ff');
    assert.equal(f.storage.get('yap-accent'), '#82b5ff');
    assert.match(f.tokens.get('--accent-ink'), /white/);
});

test('invalid saved or input colors cannot become CSS', () => {
    const f = fixture('red;display:none');
    assert.equal(f.api.accentPreference(), '#d5f879');
    f.api.setAccent('#112233'); f.api.setAccent('url(https://example.com)');
    assert.equal(f.tokens.get('--accent'), '#112233');
});

test('the second brand family and --ok derive from the accent, not from a second hard-coded hue', () => {
    const brand = readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/brand.css', import.meta.url), 'utf8');
    const declared = new Map();
    for (const block of brand.matchAll(/:root[^{]*\{([^}]*)\}/g))
        for (const [, name, value] of block[1].matchAll(/(--[a-z0-9-]+)\s*:\s*([^;}]+)/gi))
            (declared.get(name) ?? declared.set(name, []).get(name)).push(value.trim());
    const values = token => { const all = declared.get(token); assert.ok(all?.length, `${token} must be declared`); return all; };
    for (const token of ['--pink', '--pink2', '--pink-ink', '--pink-soft', '--on-pink', '--ok', '--bub-out', '--bub-out-text'])
        for (const value of values(token))
            assert.match(value, /var\(--(on-)?accent|color-mix\(in srgb,var\(--accent\)/, `${token} must follow the accent`);
    // Chrome carries no brand hue of its own: a neutral grey repeats the same channel.
    const neutral = hex => /^#(?:[0-9a-f]{3}|[0-9a-f]{6,8})$/i.test(hex) && (hex.length === 4
        ? hex[1] === hex[2] && hex[2] === hex[3]
        : hex[1] === hex[3] && hex[3] === hex[5] && hex[2] === hex[4] && hex[4] === hex[6]);
    for (const token of ['--text', '--dim', '--faint', '--border', '--border2', '--frame', '--frame-outer', '--nav', '--bub-in', '--bub-in-text'])
        for (const value of values(token))
            assert.ok(neutral(value), `${token} must be a neutral grey, got ${value}`);
});

test('applyAccent repaints every token the pink family and bubbles read through', () => {
    const f = fixture();
    f.api.setAccent('#82b5ff');
    for (const token of ['--accent', '--accent2', '--accent-ink', '--accent-soft', '--on-accent', '--bub-out', '--bub-out-text'])
        assert.ok(f.tokens.has(token), `${token} must be repainted`);
    assert.equal(f.tokens.get('--accent2'), '#82b5ff');
});
