import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

function fixture(reduced = false) {
    const events = new Map(), animations = []; let closed = 0;
    const image = { style: {}, complete: true, naturalWidth: 800, naturalHeight: 1600,
        getBoundingClientRect: () => ({ left: 0, top: 0, width: 400, height: 800 }),
        animate(frames, options) { animations.push({ frames, options }); return { cancel() {}, finished: Promise.resolve() }; } };
    const document = { getElementById: () => ({ querySelector: () => ({ getBoundingClientRect: () => ({ left: 100, top: 200, width: 100, height: 200 }) }) }) };
    const stage = { addEventListener(name, handler) { events.set(name, handler); }, setPointerCapture() {}, getBoundingClientRect: () => ({ width: 400, height: 800 }) };
    const dialog = { querySelector: selector => selector === 'img' ? image : stage, addEventListener() {}, showModal() { this.open = true; }, close() { this.open = false; } };
    const window = { yap: {}, addEventListener() {} };
    vm.runInNewContext(readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/photo-viewer.js', import.meta.url), 'utf8'), { window, document, matchMedia: () => ({ matches: reduced }), AbortController, performance });
    const api = window.yap.photoViewer;
    api.open(dialog, { invokeMethodAsync: async () => { closed++; } });
    const pointer = (type, id, x, y) => events.get(type)({ type, pointerId: id, pointerType: 'touch', clientX: x, clientY: y });
    return { api, dialog, image, pointer, animations, closed: () => closed };
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
    assert.match(f.animations[0].frames[0].transform, /translate\(-50px, -100px\) scale\(0.25, 0.25\)/);
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
});

for (const distance of [-120, 120]) test(`vertical swipe ${distance} dismisses a fitted photo`, () => {
    const f = fixture(true);
    f.pointer('pointerdown', 1, 200, 400); f.pointer('pointermove', 1, 205, 400 + distance);
    f.pointer('pointerup', 1, 205, 400 + distance);
    assert.equal(f.closed(), 1);
});
test('short vertical drag springs back, and zoomed panning does not dismiss', () => {
    const f = fixture(true);
    f.pointer('pointerdown', 1, 200, 400); f.pointer('pointermove', 1, 200, 440); f.pointer('pointerup', 1, 200, 440);
    assert.equal(f.closed(), 0); assert.equal(f.image.style.transform, 'translate(0px, 0px) scale(1)');
    f.api.zoom(f.dialog, 1);
    f.pointer('pointerdown', 2, 200, 400); f.pointer('pointermove', 2, 200, 600); f.pointer('pointerup', 2, 200, 600);
    assert.equal(f.closed(), 0);
});
