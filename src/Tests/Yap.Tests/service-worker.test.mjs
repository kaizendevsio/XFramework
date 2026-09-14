import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const source = readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/service-worker.published.js', import.meta.url), 'utf8');
function fixture({ failDownload = false } = {}) {
    const handlers = {}, downloads = [], deleted = [];
    const paths = ['index.html', '_framework/app.wasm', 'vendor/openpgp/openpgp.min.mjs', 'vendor/openpgp/LICENSE.txt',
        '_content/Bolt.Media.Browser/sframe/.gitattributes', '_content/Bolt.Media.Browser/sframe/SHA256SUMS',
        'vendor/openpgp/LICENSE', 'api/private.json', 'service-worker-assets.js'];
    const self = { importScripts() {}, assetsManifest: { version: 'new', assets: paths.map(url => ({ url, hash: 'sha256-test' })) },
        location: new URL('https://yap.test/service-worker.js'), addEventListener: (name, handler) => handlers[name] = handler };
    vm.runInNewContext(source, { self, URL, Set,
        Request: class { constructor(url, options) { this.url = url; Object.assign(this, options); } },
        caches: { open: async () => ({ addAll: async requests => { downloads.push(...requests); if (failDownload) throw Error('Integrity mismatch'); } }),
            keys: async () => ['yap-shell-old', 'yap-shell-new', 'private-media'], delete: async key => { deleted.push(key); } } });
    const run = async event => { let work; handlers[event]({ waitUntil: value => work = value }); return work; };
    return { run, downloads, deleted };
}

test('install verifies runtime assets and the served license, excluding unservable metadata', async () => {
    const f = fixture(); await f.run('install');
    assert.deepEqual(f.downloads.map(r => r.url), ['index.html', '_framework/app.wasm', 'vendor/openpgp/openpgp.min.mjs', 'vendor/openpgp/LICENSE.txt']);
    assert.ok(f.downloads.every(r => r.integrity === 'sha256-test' && r.cache === 'no-cache'));
});

test('a broken runtime download still rejects installation and preserves existing caches', async () => {
    const f = fixture({ failDownload: true });
    await assert.rejects(f.run('install'), /Integrity mismatch/);
    assert.deepEqual(f.deleted, []);
});

test('activation removes only older app shells, preserving private media caches', async () => {
    const f = fixture(); await f.run('activate');
    assert.deepEqual(f.deleted, ['yap-shell-old']);
});
