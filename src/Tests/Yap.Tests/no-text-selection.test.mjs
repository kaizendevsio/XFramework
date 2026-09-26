import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
const css = readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/mobile.css', import.meta.url), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');

test('long press never selects text, including in modal dialogs and popovers', () => {
    // Chromium's UA sheet gives modal dialogs user-select:text, so inheriting from body is not enough.
    const rule = css.match(/([^{}]+)\{-webkit-user-select:none;user-select:none;-webkit-touch-callout:none\}/);
    assert.ok(rule, 'global no-select rule');
    const selectors = rule[1].split(',').map(s => s.trim());
    for (const selector of ['body', 'dialog', '[popover]']) assert.ok(selectors.includes(selector), selector);
    assert.match(css, /input,textarea,\[contenteditable\]:not\(\[contenteditable=false\]\)\{-webkit-user-select:text;user-select:text/);
});
