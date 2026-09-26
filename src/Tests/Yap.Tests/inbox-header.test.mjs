import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
const css = readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/mobile.css', import.meta.url), 'utf8');

test('the inbox header is flat and painted with the same token as the app shell', () => {
    const shell = css.match(/\.app-shell\{background:var\((--[\w-]+)\)/)?.[1];
    const header = css.match(/\.scroll-header \.appbar\.inbox-header\{background:var\((--[\w-]+)\)!important\}/)?.[1];
    assert.ok(shell, 'app shell background token');
    assert.equal(header, shell, 'A different token shows as a darker band under the status bar.');
    assert.match(css, /\.inbox-header>\.glass-optics\{display:none\}/, 'No glass tint or blur on the inbox header.');
});
