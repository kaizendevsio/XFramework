import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const root = new URL('../../Presentation/XFramework.Yap.Client/wwwroot/', import.meta.url);
function fixture() {
    const files = new Map(), displayed = new Map();
    let conversions = 0, downloads = 0, fail = false;
    const absent = () => new DOMException('Not found', 'NotFoundError');
    const directory = {
        async getFileHandle(key, options) {
            if (!files.has(key) && !options?.create) throw absent();
            return { getFile: async () => files.get(key), createWritable: async () => ({
                async write(blob) { files.set(key, blob); }, async close() {}, async abort() {}
            }) };
        },
        async removeEntry(key) { if (!files.delete(key)) throw absent(); }
    };
    const window = { yap: {} };
    const context = { window, yap: window.yap, Blob, File, DOMException, Map, Set, Uint8Array,
        navigator: { storage: { getDirectory: async () => ({ getDirectoryHandle: async () => directory }) } },
        document: { addEventListener() {} }, addEventListener() {}, visualViewport: null,
        requestAnimationFrame() { return 1; }, cancelAnimationFrame() {},
        URL: { createObjectURL(blob) { const url = `blob:${displayed.size}`; displayed.set(url, blob); return url; }, revokeObjectURL() {} },
        async fetch() { downloads++; return { ok: true, blob: async () => new Blob(['original HEIF']) }; }
    };
    vm.createContext(context);
    vm.runInContext(readFileSync(new URL('image-previews.js', root), 'utf8'), context);
    window.yap.imagePreviews.jpeg = async () => {
        conversions++;
        if (fail) throw Error('Invalid image');
        return new Blob(['JPEG preview'], { type: 'image/jpeg' });
    };
    vm.runInContext(readFileSync(new URL('device.js', root), 'utf8'), context);
    return { api: window.yap.device, helper: window.yap.imagePreviews, files, displayed,
        stats: () => ({ conversions, downloads }), fail: value => fail = value };
}

test('HEIF preview retains original bytes and works offline from its cached JPEG', async () => {
    const f = fixture();
    const first = await f.api.mediaUrl('received', '/file', 'account', true, 'image/heif', false, 'photo.heif');
    assert.equal(f.displayed.get(first).type, 'image/jpeg');
    assert.equal(await f.files.get('received').text(), 'original HEIF');
    const offline = await f.api.mediaUrl('received', '/file', 'account', false, 'image/heif', false, 'photo.heif');
    assert.equal(await f.displayed.get(offline).text(), 'JPEG preview');
    assert.deepEqual(f.stats(), { conversions: 1, downloads: 1 });
});

test('queued HEIF previews share decoding and never request the network', async () => {
    const f = fixture();
    f.files.set('queued', new Blob(['original']));
    await Promise.all([1, 2].map(() => f.api.mediaUrl('queued', '/file', 'account', false, 'application/octet-stream', true, 'IMG.HEIC')));
    assert.deepEqual(f.stats(), { conversions: 1, downloads: 0 });
    await f.api.removeFile('queued');
    assert.equal(f.files.size, 0);
});

test('failed conversion can retry without replacing or losing the original', async () => {
    const f = fixture();
    f.files.set('queued', new Blob(['original'])); f.fail(true);
    await assert.rejects(f.api.mediaUrl('queued', '/file', 'account', false, 'image/heic', true));
    assert.equal(await f.files.get('queued').text(), 'original');
    f.fail(false);
    await f.api.mediaUrl('queued', '/file', 'account', false, 'image/heic', true);
    assert.equal(f.stats().conversions, 2);
});

test('removing an attachment during decoding does not recreate its preview on disk', async () => {
    const f = fixture();
    f.files.set('removed', new Blob(['original']));
    let finish, started;
    const decoding = new Promise(resolve => started = resolve);
    f.helper.jpeg = () => { started(); return new Promise(resolve => finish = resolve); };
    const url = f.api.mediaUrl('removed', '/file', 'account', false, 'image/heif', true);
    await decoding;
    await f.api.removeFile('removed');
    finish(new Blob(['JPEG'], { type: 'image/jpeg' }));
    await url;
    assert.equal(f.files.size, 0);
});

test('JPEG uses a bounded display copy and extension/magic recover missing HEIF MIME types', async () => {
    const f = fixture();
    f.files.set('jpg', new Blob(['JPEG']));
    const url = await f.api.mediaUrl('jpg', '/file', 'account', false, 'image/jpeg', true);
    assert.equal(await f.displayed.get(url).text(), 'JPEG preview');
    assert.equal(await f.files.get('jpg').text(), 'JPEG');
    assert.equal(f.stats().conversions, 1);
    assert.equal(await f.helper.contentType(new File(['x'], 'IMG.HEIF')), 'image/heif');
    assert.equal(await f.helper.contentType(new File(['0000ftypheic'], 'image', { type: 'application/octet-stream' })), 'image/heif');
});
