import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

function element(extra = {}) {
    const listeners = new Map();
    return { style: {}, dataset: {}, attributes: {}, listeners,
        addEventListener(name, handler) { listeners.set(name, handler); },
        setAttribute(name, value) { this.attributes[name] = value; },
        fire(name, event = {}) { listeners.get(name)?.({ type: name, ...event }); }, ...extra };
}

function fixture(reduced = false, clip = false) {
    const events = new Map(), animations = []; let closed = 0;
    const image = element({ tagName: clip ? 'VIDEO' : 'IMG', complete: true, readyState: 2, paused: true,
        naturalWidth: clip ? 0 : 800, naturalHeight: clip ? 0 : 1600, videoWidth: clip ? 800 : 0, videoHeight: clip ? 1600 : 0,
        currentTime: 0, duration: 20,
        play() { this.paused = false; this.fire('play'); return Promise.resolve(); }, pause() { this.paused = true; this.fire('pause'); },
        getBoundingClientRect: () => ({ left: 0, top: 0, width: 400, height: 800 }),
        animate(frames, options) { animations.push({ frames, options }); return { cancel() {}, finished: Promise.resolve() }; } });
    // The thumbnail reports no intrinsic size, exactly like a poster-only <video>.
    const thumbnail = element({ tagName: clip ? 'VIDEO' : 'IMG', naturalWidth: 0, naturalHeight: 0, videoWidth: 0, videoHeight: 0,
        getBoundingClientRect: () => ({ left: 100, top: 200, width: 100, height: 200 }) });
    const source = element({ querySelector: () => thumbnail });
    const document = { getElementById: id => id === 'thumb' ? source : null };
    const shade = { style: {}, animate: () => ({ finished: Promise.resolve() }) };
    const play = element(), seek = element({ value: '0' }), elapsed = element(), duration = element();
    const bar = element({ querySelector: selector => ({ '.video-play': play, '.video-seek': seek, '.video-elapsed': elapsed, '.video-duration': duration })[selector] });
    const stage = { addEventListener(name, handler) { events.set(name, handler); }, setPointerCapture() {}, getBoundingClientRect: () => ({ width: 400, height: 800 }) };
    const dialog = { querySelector: selector => selector === 'img,video' ? image : selector === '.photo-stage' ? stage : selector === '.video-transport' ? (clip ? bar : null) : shade,
        addEventListener() {}, showModal() { this.open = true; }, close() { this.open = false; } };
    const window = { yap: {}, addEventListener() {} };
    vm.runInNewContext(readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/photo-viewer.js', import.meta.url), 'utf8'), { window, document, getComputedStyle: element => ({ transform: element.style.transform || 'none', opacity: element.style.opacity || '1' }), matchMedia: () => ({ matches: reduced }), AbortController, performance });
    const api = window.yap.photoViewer;
    api.open(dialog, { invokeMethodAsync: async () => { closed++; } }, 'thumb');
    const pointer = (type, id, x, y, kind = 'touch') => events.get(type)({ type, pointerId: id, pointerType: kind, clientX: x, clientY: y });
    return { api, dialog, image, source, transport: { play, seek, elapsed, duration, bar }, pointer, animations, closed: () => closed };
}

test('two-finger pinch zooms only the photo; lifting fingers keeps zoom and reset restores fit', () => {
    const f = fixture();
    f.pointer('pointerdown', 1, 150, 400); f.pointer('pointerdown', 2, 250, 400);
    f.pointer('pointermove', 1, 100, 400); f.pointer('pointermove', 2, 300, 400);
    assert.match(f.image.style.transform, /scale\(2\)/);
    f.pointer('pointerup', 1, 100, 400); f.pointer('pointerup', 2, 300, 400);
    assert.match(f.image.style.transform, /scale\(2\)/);
    f.api.reset(f.dialog); assert.equal(f.image.style.transform, 'translate(0px, 0px) scale(1)');
    f.api.dispose(f.dialog); assert.equal(f.dialog.open, false);
});

test('zoom controls are bounded and panning cannot lose the photo', () => {
    const f = fixture();
    for (let i = 0; i < 20; i++) f.api.zoom(f.dialog, 1);
    assert.match(f.image.style.transform, /scale\(6\)/);
    f.pointer('pointerdown', 1, 200, 400); f.pointer('pointermove', 1, 5000, 5000);
    assert.equal(f.image.style.transform, 'translate(1000px, 2000px) scale(6)');
    for (let i = 0; i < 20; i++) f.api.zoom(f.dialog, -1);
    assert.equal(f.image.style.transform, 'translate(0px, 0px) scale(1)');
});


test('viewer expands from the thumbnail bounds and closes back to it', async () => {
    const f = fixture();
    assert.match(f.animations[0].frames[0].transform, /translate\(-50px, -100px\) scale\(0.25\)/);
    assert.equal(f.animations[0].frames[1].transform, 'none');
    await f.api.close(f.dialog);
    assert.equal(f.animations.length, 2);
    assert.equal(f.closed(), 1);
    await f.api.close(f.dialog);
    assert.equal(f.closed(), 1);
});

test('reduced motion opens and closes without animation', async () => {
    const f = fixture(true);
    assert.equal(f.animations.length, 0);
    await f.api.close(f.dialog);
    assert.equal(f.closed(), 1);
    assert.equal(f.source.style.opacity, '');
});

for (const distance of [-120, 120]) test(`vertical swipe ${distance} shrinks under the finger and exits without a center reset`, async () => {
    const f = fixture();
    f.pointer('pointerdown', 1, 200, 400); f.pointer('pointermove', 1, 205, 400 + distance);
    const dragged = f.image.style.transform;
    assert.match(dragged, /scale\(0.25\)/);
    assert.match(dragged, new RegExp(`${distance}px`));
    f.pointer('pointerup', 1, 205, 400 + distance);
    await f.api.close(f.dialog);
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(f.animations.at(-1).frames[0].transform, dragged);
    assert.equal(f.closed(), 1);
});
test('short vertical drag springs back, and zoomed panning does not dismiss', () => {
    const f = fixture(true);
    f.pointer('pointerdown', 1, 200, 400); f.pointer('pointermove', 1, 200, 440); f.pointer('pointerup', 1, 200, 440);
    assert.equal(f.closed(), 0); assert.equal(f.image.style.transform, 'none');
    f.api.zoom(f.dialog, 1);
    f.pointer('pointerdown', 2, 200, 400); f.pointer('pointermove', 2, 200, 600); f.pointer('pointerup', 2, 200, 600);
    assert.equal(f.closed(), 0);
});

test('the source thumbnail stays hidden through a drag and returns only when the exit lands', async () => {
    const f = fixture();
    assert.equal(f.source.style.opacity, '0');
    f.pointer('pointerdown', 1, 200, 400); f.pointer('pointermove', 1, 200, 500);
    assert.equal(f.source.style.opacity, '0');
    const closing = f.api.close(f.dialog);
    assert.equal(f.source.style.opacity, '0');
    await closing;
    assert.equal(f.source.style.opacity, '');
    assert.equal(f.closed(), 1);
});

test('tearing the viewer down mid-drag never leaves the photo hidden in the thread', () => {
    const f = fixture();
    f.pointer('pointerdown', 1, 200, 400); f.pointer('pointermove', 1, 200, 520);
    assert.equal(f.source.style.opacity, '0');
    f.api.dispose(f.dialog);
    assert.equal(f.source.style.opacity, '');
});

test('a swipe dismissal restores the thumbnail once, even though dispose follows', async () => {
    const f = fixture();
    f.pointer('pointerdown', 1, 200, 400); f.pointer('pointermove', 1, 200, 520); f.pointer('pointerup', 1, 200, 520);
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(f.closed(), 1);
    assert.equal(f.source.style.opacity, '');
    f.source.style.opacity = 'keep';
    f.api.dispose(f.dialog);
    assert.equal(f.source.style.opacity, 'keep');
});

test('a clip opens from its thumbnail, autoplays, and taps drive playback instead of zoom', async () => {
    const f = fixture(false, true);
    assert.match(f.animations[0].frames[0].transform, /scale\(0.25\)/);
    assert.equal(f.source.style.opacity, '0');
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(f.image.paused, false);
    assert.equal(f.transport.bar.dataset.playing, 'true');
    f.pointer('pointerdown', 1, 200, 400); f.pointer('pointerup', 1, 200, 400);
    assert.equal(f.image.paused, true);
    f.pointer('pointerdown', 1, 200, 400); f.pointer('pointerup', 1, 200, 400);
    assert.equal(f.image.paused, false);
    await f.api.close(f.dialog);
    assert.equal(f.source.style.opacity, '');
});

test('clip transport reports time, scrubs, and stops playback on dispose', () => {
    const f = fixture(false, true);
    assert.equal(f.transport.elapsed.textContent, '0:00');
    assert.equal(f.transport.duration.textContent, '0:20');
    f.transport.seek.value = '500';
    f.transport.seek.fire('input');
    assert.equal(f.image.currentTime, 10);
    f.image.currentTime = 15; f.image.fire('timeupdate');
    assert.equal(f.transport.elapsed.textContent, '0:15');
    assert.equal(f.transport.seek.value, '750');
    f.transport.play.fire('click');
    f.api.dispose(f.dialog);
    assert.equal(f.image.paused, true);
});
