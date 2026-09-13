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
