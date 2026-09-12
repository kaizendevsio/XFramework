import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

function fixture() {
    const events = new Map(); const image = { style: {} };
    const stage = { addEventListener(name, handler) { events.set(name, handler); }, setPointerCapture() {}, getBoundingClientRect: () => ({ width: 400, height: 800 }) };
    const dialog = { querySelector: selector => selector === 'img' ? image : stage, addEventListener() {}, showModal() { this.open = true; }, close() { this.open = false; } };
    const window = { yap: {}, addEventListener() {} };
    vm.runInNewContext(readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/photo-viewer.js', import.meta.url), 'utf8'), { window, AbortController, performance });
    const api = window.yap.photoViewer;
    api.open(dialog, { invokeMethodAsync: async () => {} });
    const pointer = (type, id, x, y) => events.get(type)({ type, pointerId: id, pointerType: 'touch', clientX: x, clientY: y });
    return { api, dialog, image, pointer };
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
