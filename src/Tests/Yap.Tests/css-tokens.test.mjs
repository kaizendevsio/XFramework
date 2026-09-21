// A `var(--typo)` with no fallback is not an error anywhere: the declaration is dropped at compute
// time and the property silently falls back to its inherited or initial value. `background:var(--bg)`
// on a full-screen <dialog> that way is a transparent dialog, which is how the whole conversation
// came to show through a live video call. Three of these have now shipped (--bg, --muted,
// --text-muted), so the class gets a test rather than another pair of eyes.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { relative } from 'node:path';

const client = new URL('../../Presentation/XFramework.Yap.Client/', import.meta.url);

/** Every file the browser loads that can declare or read a custom property. */
function sources() {
    const files = [];
    const walk = (dir, keep) => {
        for (const entry of readdirSync(dir, { withFileTypes: true })) {
            const url = new URL(entry.name + (entry.isDirectory() ? '/' : ''), dir);
            if (entry.isDirectory()) { if (entry.name !== 'vendor' && entry.name !== 'bin' && entry.name !== 'obj' && entry.name !== '_framework') walk(url, keep); }
            else if (keep(entry.name)) files.push(url);
        }
    };
    walk(client, name => name.endsWith('.razor.css') || name.endsWith('.razor'));
    walk(new URL('wwwroot/', client), name => name.endsWith('.css') || name.endsWith('.js'));
    return files;
}

const name = url => relative(new URL('.', client).pathname, url.pathname).replace(/\\/g, '/');
// Comments hold dead rules and prose; scanning them reports properties nothing ever computes.
const uncomment = text => text.replace(/\/\*[\s\S]*?\*\//g, ' ');

/** `--x:` in a declaration position - not the `--x` inside a `var()`. */
const DECLARED = /(?:^|[;{\s"'])(--[\w-]+)\s*:/g;
/** `setProperty('--x', …)`: a token CSS reads but only script ever writes. */
const SCRIPTED = /setProperty\(\s*['"`](--[\w-]+)['"`]/g;
/** `var(--x)` with nothing after the name: no fallback, so a missing declaration is invisible. */
const UNFALLBACKED = /var\(\s*(--[\w-]+)\s*\)/g;

/**
 * Tokens that are deliberately read before anything declares them. Each needs a reason, because the
 * alternative - a blanket skip - is the hole this test exists to close.
 */
const ALLOWED = new Map([
    // Empty on purpose. Tokens only script ever writes - --glass-filter, --yap-keyboard, --band and
    // the rest - are picked up from their setProperty() call above, so they never belong here. Add
    // an entry only for a token that genuinely has no declaration anywhere, with the reason why the
    // fallback-to-initial behaviour is the intended one.
]);

test('every var(--x) without a fallback has a declaration somewhere the browser loads', () => {
    const declared = new Set(), used = new Map();
    for (const file of sources()) {
        const text = uncomment(readFileSync(file, 'utf8'));
        for (const [, token] of text.matchAll(DECLARED)) declared.add(token);
        for (const [, token] of text.matchAll(SCRIPTED)) declared.add(token);
        if (file.pathname.endsWith('.js')) continue;
        for (const [, token] of text.matchAll(UNFALLBACKED))
            (used.get(token) ?? used.set(token, new Set()).get(token)).add(name(file));
    }

    const missing = [...used]
        .filter(([token]) => !declared.has(token) && !ALLOWED.has(token))
        .map(([token, files]) => `${token} — read in ${[...files].sort().join(', ')}`)
        .sort();
    assert.deepEqual(missing, [],
        `undefined CSS custom properties (declare them, give the var() a fallback, or allow-list with a reason):\n  ${missing.join('\n  ')}`);
});

test('the allow-list only holds tokens that are really undeclared', () => {
    const declared = new Set();
    for (const file of sources()) {
        const text = uncomment(readFileSync(file, 'utf8'));
        for (const [, token] of text.matchAll(DECLARED)) declared.add(token);
        for (const [, token] of text.matchAll(SCRIPTED)) declared.add(token);
    }
    const stale = [...ALLOWED.keys()].filter(token => declared.has(token));
    assert.deepEqual(stale, [], `these are declared now, so drop them from ALLOWED: ${stale.join(', ')}`);
});

// The call surface covers the app with showModal(). Whatever paints it has to be an opaque token in
// both themes - the bug was a background that resolved to nothing at all.
test('the call dialog paints an opaque background from a declared token', () => {
    const mobile = uncomment(readFileSync(new URL('wwwroot/mobile.css', client), 'utf8'));
    const rule = mobile.split('\n').find(line => line.startsWith('.voice-dialog{'));
    assert.ok(rule, '.voice-dialog rule not found in mobile.css');
    const background = /background:\s*var\(\s*(--[\w-]+)/.exec(rule);
    assert.ok(background, '.voice-dialog must set a background: without one the conversation shows through the call');

    const app = uncomment(readFileSync(new URL('wwwroot/app.css', client), 'utf8'));
    const brand = uncomment(readFileSync(new URL('wwwroot/brand.css', client), 'utf8'));
    for (const [source, text] of [['app.css', app], ['brand.css', brand]]) {
        const themes = [...text.matchAll(new RegExp(`${background[1]}\\s*:\\s*([^;}]+)`, 'g'))].map(m => m[1].trim());
        assert.ok(themes.length >= 2, `${background[1]} needs a light and a dark value in ${source}, found ${themes.length}`);
        for (const value of themes) assert.doesNotMatch(value, /transparent|rgba?\([^)]*[,/ ]\s*0?\.\d+\s*\)/, `${background[1]} must be opaque, got ${value}`);
    }
});
