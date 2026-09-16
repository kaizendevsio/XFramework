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
        document: { baseURI: 'https://yap.test/', addEventListener() {} }, addEventListener() {}, visualViewport: null,
        requestAnimationFrame() { return 1; }, cancelAnimationFrame() {},
        location: { href: 'https://yap.test/chat/one' },
        URL: class extends URL { static createObjectURL(blob) { const url = `blob:${displayed.size}`; displayed.set(url, blob); return url; } static revokeObjectURL() {} },
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

test('a staged video too large to copy still previews from the held file handle', async () => {
    const f = fixture();
    // Past the stage limit pickFile keeps the File instead of copying it into OPFS; the
    // composer shows that thumbnail in place of a filename chip, so it must still resolve.
    const picked = await f.api.pickFile({ files: [new File(['movie'], 'IMG_0042.MOV', { type: '' })] }, 'staged', 1, 1024);
    assert.equal(picked.staged, false);
    assert.equal(f.files.size, 0);
    const url = await f.api.mediaUrl('staged', '/file', 'account', true, 'video/quicktime', true, 'IMG_0042.MOV');
    assert.equal(await f.displayed.get(url).text(), 'movie');
    assert.deepEqual(f.stats(), { conversions: 0, downloads: 0 });
    await f.api.removeFile('staged');
    await assert.rejects(f.api.mediaUrl('staged', '/file', 'account', true, 'video/quicktime', true, 'IMG_0042.MOV'));
});

test('received video streams through the account-bound route without buffering a file', async () => {
    const f = fixture();
    const url = await f.api.mediaUrl('large', 'api/chat/video', 'tenant:user', true, 'video/quicktime', false, 'IMG.MOV');
    assert.equal(url, '/api/chat/video?account=tenant%3Auser&mediaType=video%2Fquicktime');
    assert.equal(f.files.size, 0);
    assert.deepEqual(f.stats(), { conversions: 0, downloads: 0 });
});
