import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const source = readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/branding.js', import.meta.url), 'utf8');
const defaults = { name: 'Yap', shortName: 'Yap', tagline: 'A little closer.', logoUrl: '', accentColor: '#d5f879',
    icon192Url: '/yap-app-v2-192.png', icon512Url: '/yap-app-v2-512.png', appleIconUrl: '/yap-app-v2-apple.png', manifestUrl: '/manifest.webmanifest' };
const brand = (name, color) => ({ ...defaults, name, shortName: name, accentColor: color, manifestUrl: '/api/branding/manifest' });

async function fixture(response, saved = [], offline = false) {
    const storage = new Map(saved), attributes = new Map(), requests = [];
    const nodes = new Map();
    const document = { title: '', querySelector(selector) {
        if (!nodes.has(selector)) nodes.set(selector, { setAttribute: (name, value) => attributes.set(`${selector}:${name}`, value), removeAttribute() {}, replaceChildren() {} });
        return nodes.get(selector);
    }, createElement: () => ({}) };
    let accent;
    const yap = { accentPreference: () => '#d5f879', applyAccent: value => { accent = value; } };
    vm.runInNewContext(source, { yap, document, AbortController, setTimeout, clearTimeout,
        localStorage: { getItem: key => storage.get(key), setItem: (key, value) => storage.set(key, value) },
        fetch: async (url, options) => { requests.push({ url, options }); if (offline) throw new Error('offline'); return { ok: true, json: async () => response }; }
    });
    await yap.branding.refresh();
    const value = yap.branding.get();
    return { value, storage, document, attributes, requests, yap, accent: () => accent };
}

test('two origins apply independent public branding and network-only manifests', async () => {
    const alpha = await fixture(brand('Alpha', '#336699'));
    const beta = await fixture(brand('Beta', '#cceeff'));
    assert.equal(alpha.value.name, 'Alpha'); assert.equal(beta.value.name, 'Beta');
    assert.equal(alpha.accent(), '#336699'); assert.equal(beta.accent(), '#cceeff');
    assert.equal(alpha.attributes.get('link[rel="manifest"]:href'), '/api/branding/manifest');
    assert.equal(alpha.attributes.get('meta[name="apple-mobile-web-app-title"]:content'), 'Alpha');
    assert.equal(alpha.requests[0].url, '/api/branding'); assert.equal(alpha.requests[0].options.cache, 'no-store');
    assert.match(alpha.document.title, /^Alpha/);
    assert.equal(JSON.parse(beta.storage.get('yap-branding')).name, 'Beta');
});

test('startup branding returns synchronously while its network refresh is still pending', () => {
    const yap = { applyAccent() {} };
    vm.runInNewContext(source, { yap, document: { querySelector: () => null }, AbortController,
        setTimeout: () => 0, clearTimeout() {}, localStorage: { getItem: () => null },
        fetch: () => new Promise(() => {}) });
    assert.equal(yap.branding.get().name, 'Yap');
    assert.equal(typeof yap.branding.get().then, 'undefined');
});

test('offline startup uses this origin public cache and preserves the user accent preference', async () => {
    const alpha = brand('Alpha', '#336699');
    const f = await fixture(null, [['yap-branding', JSON.stringify(alpha)], ['yap-accent', '#abcdef']], true);
    assert.equal(f.value.name, 'Alpha'); assert.equal(f.accent(), '#abcdef');
    assert.equal(f.yap.accentPreference(), '#abcdef');
    const empty = await fixture(null, [], true);
    assert.equal(empty.value.name, 'Yap');
    assert.equal(empty.attributes.get('link[rel="manifest"]:href'), '/manifest.webmanifest');
});

test('untrusted cached or returned URLs and CSS cannot become branding', async () => {
    for (const invalid of [{ logoUrl: '//external.example/logo.png' }, { accentColor: 'red;display:none' },
        { icon192Url: '/images/../secret.png' }, { manifestUrl: 'https://external.example/manifest' }]) {
        const value = { ...brand('Bad', '#336699'), ...invalid };
        const f = await fixture(value, [['yap-branding', JSON.stringify(value)]]);
        assert.equal(f.value.name, 'Yap'); assert.equal(f.accent(), '#d5f879');
    }
});

test('public cache only persists allowlisted presentation fields', async () => {
    const f = await fixture({ ...brand('Alpha', '#336699'), accessToken: 'must-not-persist', tenantId: 'must-not-persist' });
    const stored = f.storage.get('yap-branding');
    assert.ok(!stored.includes('accessToken')); assert.ok(!stored.includes('tenantId'));
});
