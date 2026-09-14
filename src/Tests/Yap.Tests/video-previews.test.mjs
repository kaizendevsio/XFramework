import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import vm from 'node:vm';

const source = (await fs.readFile(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/video-previews.js', import.meta.url), 'utf8'))
    .replaceAll('export function ', 'function ');
function fixture() {
    let active = 0, maximum = 0, created = 0;
    const revoked = [], sizes = [];
    class Video extends EventTarget {
        duration = 4; videoWidth = 1920; videoHeight = 1080; src = 'clip'; poster = '';
        getAttribute() { return this.src; }
        removeAttribute(name) { this[name] = ''; }
        set currentTime(value) { queueMicrotask(() => this.dispatchEvent(new Event('seeked'))); }
        load() {
            if (!this.src) { if (this.started) active--; this.started = false; return; }
            this.started = true; maximum = Math.max(maximum, ++active);
            if (this.src !== 'stalled') queueMicrotask(() => this.dispatchEvent(new Event('loadedmetadata')));
        }
        pause() {}
    }
    const context = vm.createContext({ AbortController, DOMException, setTimeout, clearTimeout,
        URL: { createObjectURL: () => `blob:poster-${++created}`, revokeObjectURL: url => revoked.push(url) },
        document: { createElement: tag => tag === 'video' ? new Video() : {
            getContext: () => ({ drawImage() {} }),
            toBlob(callback) { sizes.push([this.width, this.height]); callback({}); }
        } }
    });
    vm.runInContext(source, context);
    return { context, Video, revoked, sizes, state: () => ({ active, maximum, created }),
        finish: () => vm.runInContext('queue', context) };
}

test('mounted clips get bounded posters, release URLs, and decode serially', async () => {
    const f = fixture(), first = new f.Video(), second = new f.Video();
    f.context.mount(first); f.context.mount(second);
    await f.finish();
    assert.equal(first.poster, 'blob:poster-1');
    assert.equal(second.poster, 'blob:poster-2');
    assert.deepEqual(f.sizes, [[640, 360], [640, 360]]);
    assert.deepEqual(f.state(), { active: 0, maximum: 1, created: 2 });
    f.context.release(first); f.context.release(second);
    assert.deepEqual(f.revoked, ['blob:poster-1', 'blob:poster-2']);
});

test('unmounting a stalled clip cancels its decoder and lets the next preview finish', async () => {
    const f = fixture(), stalled = new f.Video(), next = new f.Video();
    stalled.src = 'stalled';
    f.context.mount(stalled); f.context.mount(next);
    await Promise.resolve();
    f.context.release(stalled);
    await f.finish();
    assert.equal(stalled.poster, '');
    assert.equal(next.poster, 'blob:poster-1');
    assert.deepEqual(f.state(), { active: 0, maximum: 1, created: 1 });
    f.context.release(next);
});
