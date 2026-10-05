import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

function fixture(saved = {}) {
    const storage = new Map(Object.entries(saved));
    const window = {};
    const context = vm.createContext({ window, document: { documentElement: { dataset: {}, style: { setProperty() {} } }, querySelector: () => ({}) },
        localStorage: { getItem: key => storage.get(key) ?? null, setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) } });
    const source = readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/app.js', import.meta.url), 'utf8').split('// Focus rings')[0];
    vm.runInContext(source, context); context.yap = window.yap;
    return { api: window.yap, storage };
}

test('a user who never chose a video quality follows the default (0 for each half)', () => {
    assert.deepEqual([...fixture().api.videoPreference()], [0, 0]);
});

test('the old default pair (1440p, 60 fps) cannot be told from never chosen: it migrates to the default', () => {
    const f = fixture({ 'yap-video-quality': JSON.stringify([1440, 60]) });
    assert.deepEqual([...f.api.videoPreference()], [0, 0]);
    assert.equal(f.storage.has('yap-video-quality'), false, 'the old key is gone');
    assert.deepEqual(JSON.parse(f.storage.get('yap-video-preference')), { height: 0, fps: 0 });
});

test('an old choice away from the old default is kept, half by half', () => {
    assert.deepEqual([...fixture({ 'yap-video-quality': JSON.stringify([720, 60]) }).api.videoPreference()], [720, 0]);
    assert.deepEqual([...fixture({ 'yap-video-quality': JSON.stringify([1440, 30]) }).api.videoPreference()], [0, 30]);
    assert.deepEqual([...fixture({ 'yap-video-quality': JSON.stringify([2160, 30]) }).api.videoPreference()], [2160, 30]);
});

test('setting one half records only that half', () => {
    const f = fixture();
    f.api.setVideoPreference(0, 60);
    assert.deepEqual([...f.api.videoPreference()], [0, 60], 'the height still follows the default');
    f.api.setVideoPreference(1440, 0);
    assert.deepEqual([...f.api.videoPreference()], [1440, 60]);
});

test('corrupt or unavailable storage falls back to the default', () => {
    assert.deepEqual([...fixture({ 'yap-video-preference': '{not json' }).api.videoPreference()], [0, 0]);
});
