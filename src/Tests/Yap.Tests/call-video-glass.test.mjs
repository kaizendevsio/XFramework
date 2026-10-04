// Nothing on the video call screen may filter its backdrop. A backdrop-filter over live video has to blur the
// picture under it again on every video frame, and WebKit re-composites the stage to do it: an iPhone in a long
// video call ran hot with the control tray, name chip and tile labels all doing that 30 times a second. The voice
// screen sits over a still backdrop and keeps its glass; this checks the video screen cannot quietly get it back.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const client = new URL('../../Presentation/XFramework.Yap.Client/', import.meta.url);
const read = path => readFileSync(new URL(path, client), 'utf8');
const uncomment = text => text.replace(/\/\*[\s\S]*?\*\//g, ' ');

/** Innermost `selector { body }` blocks: rules inside @media come out as plain rules. */
function rules(css, sheet) {
    const out = [];
    for (const match of uncomment(css).matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
        const selectors = match[1].split(',').map(x => x.trim()).filter(Boolean);
        out.push({ sheet, selectors, body: match[2], order: out.length });
    }
    return out;
}

const blurs = body => /(?:^|;|\s)(?:-webkit-)?backdrop-filter\s*:(?!\s*none\b)[^;]+/.test(body);
const clears = body => /(?:^|;|\s)backdrop-filter\s*:\s*none\b/.test(body) && /-webkit-backdrop-filter\s*:\s*none\b/.test(body);
const classesOf = selector => [...selector.replace(/::?[\w-]+(\([^)]*\))?/g, ' ').matchAll(/\.([\w-]+)/g)].map(x => x[1]);
/** (attributes and pseudo-classes, classes) collapsed into one number: enough for class-only selectors. */
const specificity = selector => (selector.match(/\.[\w-]+|\[[^\]]+\]|:(?!:)[\w-]+/g) ?? []).length;

/** Every class the video screen can render, including the shared fragments it pulls in. */
function videoClasses() {
    const razor = read('Components/VoiceCall.razor');
    const start = razor.indexOf('else if (Video)');
    const end = razor.indexOf('call-screen voicescreen @(Voice.Incoming', start);
    assert.ok(start > 0 && end > start, 'the video branch of VoiceCall.razor moved; update this test');
    const fragments = ['private RenderFragment Notices', 'private RenderFragment CameraPicker', 'private RenderFragment AudioOutputPicker']
        .map(name => razor.slice(razor.indexOf(name), razor.indexOf('</div>;', razor.indexOf(name))));
    const markup = [razor.slice(start, end), ...fragments, read('Components/VideoDiagnosticsPanel.razor')].join('\n');
    const classes = new Set();
    for (const attribute of markup.matchAll(/class="([^"]*(?:"[^"]*"[^"]*)*?)"/g))
        for (const word of attribute[1].matchAll(/[a-z][a-z0-9-]*/g)) classes.add(word[0]);
    return classes;
}

const scoped = rules(read('Components/VoiceCall.razor.css'), 'VoiceCall.razor.css');
const global = ['app.css', 'glass.css', 'mobile.css', 'motion.css', 'brand.css']
    .flatMap(file => rules(read(`wwwroot/${file}`), file));

test('the video screen markup is where this test looks', () => {
    const classes = videoClasses();
    for (const name of ['vidscreen', 'call-tray', 'call-chip', 'call-round', 'pip', 'tile-name', 'call-notice'])
        assert.ok(classes.has(name), `${name} is rendered on the video screen`);
});

test('no rule written for the video screen filters its backdrop', () => {
    const video = /\.(?:vidscreen|vidgrid|vidtile|vidtop|pip|stage-caption)\b/;
    const offenders = [...scoped, ...global].flatMap(rule => rule.selectors
        .filter(selector => video.test(selector) && blurs(rule.body))
        .map(selector => `${rule.sheet}: ${selector}`));
    assert.deepEqual(offenders, []);
});

test('every glass surface that can sit over video has its blur cleared there', () => {
    const classes = videoClasses();
    const missing = [];
    for (const rule of [...scoped, ...global]) {
        if (!blurs(rule.body)) continue;
        for (const selector of rule.selectors) {
            const needed = classesOf(selector);
            // A selector that names a class the video screen never renders (.voicescreen, .cbtn) cannot match there.
            if (needed.length === 0 || !needed.every(name => classes.has(name))) continue;
            const subject = classesOf(selector.split(/\s+|>|\+|~/).filter(Boolean).at(-1)).at(-1);
            const cleared = scoped.some(other => other.sheet === 'VoiceCall.razor.css' && clears(other.body) &&
                other.selectors.some(candidate => candidate.startsWith('.vidscreen ') && classesOf(candidate).at(-1) === subject &&
                    (specificity(candidate) > specificity(selector) || (specificity(candidate) === specificity(selector) &&
                        rule.sheet === other.sheet && other.order > rule.order))));
            if (!cleared) missing.push(`${rule.sheet}: ${selector}`);
        }
    }
    assert.deepEqual(missing, [], 'these would blur live video: clear them under .vidscreen');
});

test('the surfaces keep a visible tint once the blur is gone', () => {
    const vidscreen = scoped.find(rule => rule.selectors.includes('.vidscreen') && /--call-tray/.test(rule.body));
    const alpha = name => Number(vidscreen.body.match(new RegExp(`${name}:\\s*rgba\\([^)]*,\\s*([\\d.]+)\\)`))?.[1]);
    assert.ok(alpha('--call-glass') >= 0.55, 'the chip and round buttons stay legible over a bright picture');
    assert.ok(alpha('--call-tray') >= 0.55, 'so does the control tray');
});

test('the voice screen, over a still backdrop, keeps its glass', () => {
    const tray = scoped.find(rule => rule.selectors.includes('.call-tray'));
    assert.ok(blurs(tray.body));
    assert.ok(!scoped.some(rule => rule.selectors.some(x => x.startsWith('.voicescreen')) && clears(rule.body)));
});
