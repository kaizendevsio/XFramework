import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
const source = readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/microphone.js', import.meta.url), 'utf8');
function fixture(navigator) {
    const window = { yap: {} }; vm.runInNewContext(source, { window, navigator }); return window.yap.microphone;
}
test('checking permission never opens a microphone; the explicit test releases every track', async () => {
    let opened = 0, stopped = 0;
    const mic = fixture({ mediaDevices: { getUserMedia: async () => {
        opened++; return { getTracks: () => [{ stop: () => stopped++ }] };
    } }, permissions: { query: async () => ({ state: 'prompt' }) } });
    assert.equal(await mic.state(), 'prompt'); assert.equal(opened, 0);
    assert.equal(await mic.request(), 'ready'); assert.equal(opened, 1); assert.equal(stopped, 1);
    assert.equal(await mic.state(), 'prompt', 'a successful capture must not pretend permission is permanent');
});
test('unsupported permission queries and denied capture have useful states', async () => {
    const mic = fixture({ mediaDevices: { getUserMedia: async () => { throw { name: 'NotAllowedError' }; } } });
    assert.equal(await mic.state(), 'unknown'); assert.equal(await mic.request(), 'denied');
    assert.equal(await fixture({}).request(), 'unsupported');
});
