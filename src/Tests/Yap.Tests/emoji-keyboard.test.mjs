import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const source = readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/emoji-keyboard.js', import.meta.url), 'utf8');

// Values produced inside the vm carry the vm's prototypes; compare them by value.
const plain = value => JSON.parse(JSON.stringify(value));

function fixture({ saved = {}, measured = '' } = {}) {
    const storage = new Map(Object.entries(saved)), properties = new Map();
    if (measured) properties.set('--yap-keyboard', measured);
    const root = {
        dataset: {},
        style: { setProperty: (key, value) => properties.set(key, value), getPropertyValue: key => properties.get(key) ?? '' }
    };
    const window = { yap: {} };
    vm.runInNewContext(source, {
        window, document: { documentElement: root },
        localStorage: { getItem: key => storage.has(key) ? storage.get(key) : null, setItem: (key, value) => storage.set(key, String(value)) }
    });
    return { api: window.yap.emojiKeyboard, root, properties, storage };
}

// The picker stands exactly where the keyboard did; a wrong height here is the composer jumping.
test('the picker takes the last measured keyboard height', () => {
    assert.equal(fixture({ saved: { 'yap-keyboard-height': '336' } }).properties.get('--yap-keyboard'), '336px');
});

test('an unmeasured device gets a plausible phone keyboard', () => {
    assert.equal(fixture().properties.get('--yap-keyboard'), '300px');
});

test('a nonsense or out of range measurement is ignored', () => {
    for (const value of ['0', '4000', 'tall', '']) assert.equal(fixture({ saved: { 'yap-keyboard-height': value } }).properties.get('--yap-keyboard'), '300px');
});

test('a live measurement is never overwritten by the remembered one', () => {
    assert.equal(fixture({ saved: { 'yap-keyboard-height': '336' }, measured: '291px' }).properties.get('--yap-keyboard'), '291px');
});

test('showing flags the document before dropping the keyboard, hiding reverses it', () => {
    const { api, root } = fixture();
    const order = [];
    const textarea = { blur: () => order.push(`blur:${root.dataset.emoji}`), focus: () => order.push('focus') };
    api.show(textarea);
    assert.equal(root.dataset.emoji, 'open');
    api.hide(textarea, false);
    assert.equal(root.dataset.emoji, undefined);
    api.show(textarea);
    api.hide(textarea, true);
    // The flag lands first so the swap is one layout commit, and only a deliberate close refocuses.
    assert.deepEqual(order, ['blur:open', 'blur:open', 'focus']);
});

test('the caret is read from the blurred textarea and put back without focusing', () => {
    const { api } = fixture();
    const textarea = { value: 'hello world', selectionStart: 5, selectionEnd: 5, focused: false, focus() { this.focused = true; }, setSelectionRange(start, end) { this.selectionStart = start; this.selectionEnd = end; } };
    assert.deepEqual(plain(api.caret(textarea)), [5, 5]);
    textarea.selectionEnd = 11;
    assert.deepEqual(plain(api.caret(textarea)), [5, 11]);
    api.place(textarea, 7);
    assert.deepEqual([textarea.selectionStart, textarea.selectionEnd, textarea.focused], [7, 7, false]);
    assert.deepEqual(plain(api.caret(null)), [0, 0]);
});

test('recently used emoji persist per device, most recent first and deduplicated', () => {
    const { api, storage } = fixture();
    assert.deepEqual(plain(api.recent()), []);
    api.use('😀');
    assert.deepEqual(plain(api.use('🎉')), ['🎉', '😀']);
    assert.deepEqual(plain(api.use('😀')), ['😀', '🎉']);
    assert.deepEqual(JSON.parse(storage.get('yap-emoji-recent')), ['😀', '🎉']);
    for (let n = 0; n < 40; n++) api.use(`e${n}`);
    assert.equal(api.recent().length, 24);
});

test('a corrupted history reads as empty rather than breaking the picker', () => {
    assert.deepEqual(plain(fixture({ saved: { 'yap-emoji-recent': '{oops' } }).api.recent()), []);
    assert.deepEqual(plain(fixture({ saved: { 'yap-emoji-recent': '[1,null,"😀"]' } }).api.recent()), ['😀']);
});
