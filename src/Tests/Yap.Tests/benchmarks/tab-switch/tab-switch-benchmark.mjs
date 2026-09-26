// Manual N-cycle tab-switch memory benchmark, run against the local browser fixture only.
//
//   YAP_FIXTURE_ENCRYPTION=1 YAP_FIXTURE_PORT=5191 [YAP_FIXTURE_WEBROOT=<published wwwroot>] \
//     src/Tests/Yap.Tests/bin/Debug/net10.0/Yap.Tests.exe --serve
//   npm i playwright-core   (in any scratch folder; not a repository dependency)
//   node tab-switch-benchmark.mjs [cycles=200] [base=https://127.0.0.1:5191]
//
// One cycle is Inbox -> Calls -> Saved -> Settings -> Inbox. Checkpoints print one JSON row
// each: JS heap, WebAssembly memory, DOM/listener/observer counts, SVG filters, backdrop
// layers, blob URLs, timers, idle rAF, view transitions and switch latency. ENGINE=webkit runs
// Playwright WebKit (with an in-memory OPFS stand-in, since Windows WebKit ships none) and, on
// Windows, samples the WebKit web and GPU process memory. The run exits non-zero if anything
// that should return to its baseline keeps growing. Desktop engines approximate an iPhone;
// they do not reproduce iOS jetsam limits.
import { chromium, webkit } from 'playwright-core';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';

const engine = process.env.ENGINE || 'chromium';
const cycles = Number(process.argv[2] || 200);
const base = process.argv[3] || 'https://127.0.0.1:5191';
if (!/^https?:\/\/(127\.0\.0\.1|localhost)(:\d+)?$/.test(base)) throw new Error('Run only against the local browser fixture.');
const here = p => new URL(p, import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1');
const checkpoints = new Set([0, 10, 25, 50, 100, 150, 200, 300, 400].filter(n => n <= cycles));
const tabs = ['/calls', '/saved', '/settings', '/'];
// WebKit refuses more than 100 pushState calls per 10 s; a person cannot tap that fast anyway.
const pause = Number(process.env.WAIT_MS ?? (engine === 'webkit' ? 60 : 0));

const browser = engine === 'webkit'
  ? await webkit.launch({ headless: true, executablePath: process.env.WEBKIT_PATH })
  : await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_PATH, args: ['--enable-precise-memory-info', '--ignore-certificate-errors'] });
const context = await browser.newContext({ viewport: { width: 430, height: 932 }, deviceScaleFactor: Number(process.env.DPR || (engine === 'webkit' ? 3 : 1)),
  ignoreHTTPSErrors: true, serviceWorkers: engine === 'webkit' ? 'block' : 'allow' });
if (engine === 'webkit') {
  const shim = fs.readFileSync(here('./opfs-shim.js'), 'utf8');
  await context.addInitScript({ content: shim });
  await context.route(/sqlite-wasm-worker\.js/, async route => {
    const response = await route.fetch();
    await route.fulfill({ response, body: shim + '\n' + await response.text(), headers: { ...response.headers(), 'content-encoding': '' } });
  });
}
await context.addInitScript({ path: here('./instrument.js') });
// A/B switches for bisecting: force view transitions off, or strip backdrop filters.
if (process.env.NO_VT) await context.addInitScript({ content: 'document.startViewTransition = undefined; try { Object.defineProperty(Document.prototype, "startViewTransition", { value: undefined, configurable: true }); } catch {}' });
if (process.env.NO_BACKDROP) await context.addInitScript({ content: 'addEventListener("DOMContentLoaded", () => { const s = document.createElement("style"); s.textContent = "*,*::before,*::after{backdrop-filter:none!important;-webkit-backdrop-filter:none!important}"; document.head.append(s); });' });

function processMemory() {
  if (engine !== 'webkit' || process.platform !== 'win32') return {};
  const out = execFileSync('powershell', ['-NoProfile', '-Command', "Get-Process WebKitWebProcess,WebKitGPUProcess -ErrorAction SilentlyContinue | ForEach-Object { $_.Name + ' ' + $_.WorkingSet64 + ' ' + $_.PrivateMemorySize64 }"], { encoding: 'utf8' });
  const result = {};
  for (const line of out.trim().split(/\r?\n/).filter(Boolean)) {
    const [name, ws, priv] = line.split(' '), key = name.replace('WebKit', '');
    result[key + 'WsMB'] = Math.round((result[key + 'WsMB'] || 0) + ws / 1048576);
    result[key + 'PrivMB'] = Math.round((result[key + 'PrivMB'] || 0) + priv / 1048576);
  }
  return result;
}

const page = await context.newPage();
page.on('pageerror', e => console.error('pageerror', e.message));
const api = {};
page.on('request', r => { const u = new URL(r.url()); if (u.pathname.startsWith('/api/')) { const k = u.pathname.replace(/[0-9a-f-]{36}/g, ':id'); api[k] = (api[k] || 0) + 1; } });
let loads = 0;
page.on('load', () => loads++);
const cdp = engine === 'webkit' ? null : await context.newCDPSession(page);
await cdp?.send('Performance.enable');

await page.goto(base + '/login');
await page.waitForSelector('input[autocomplete="username"]', { timeout: 120000 });
// Fixture-only test account from UiFixture.cs; never a real account.
await page.fill('input[autocomplete="username"]', 'fixture');
await page.fill('input[type="password"]', process.env.YAP_FIXTURE_PASSWORD || 'fixture');
await page.click('button[type="submit"]');
await page.waitForSelector('.bottomnav', { timeout: 60000 });
if (new URL(page.url()).pathname !== '/') await page.evaluate(() => window.__nav('/'));
await page.waitForSelector('.inbox-panel .row', { timeout: 60000 });
await page.waitForTimeout(3000);
const loadBaseline = loads;

async function measure(n, latencies) {
  let heap = { usedSize: 0 }, counters = {};
  if (cdp) for (let i = 0; i < 3; i++) await cdp.send('HeapProfiler.collectGarbage');
  await page.waitForTimeout(engine === 'webkit' ? 2000 : 300);
  const idleRaf = await page.evaluate(() => window.__idleRaf(1000));
  if (cdp) { heap = await cdp.send('Runtime.getHeapUsage'); counters = await cdp.send('Memory.getDOMCounters'); }
  const snap = await page.evaluate(() => window.__snapshot());
  const sorted = [...latencies].sort((a, b) => a - b);
  const pct = p => sorted.length ? Math.round(sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * p))]) : null;
  return { cycles: n, jsHeapMB: +(heap.usedSize / 1048576).toFixed(2), wasmMB: snap.wasm ? +(snap.wasm / 1048576).toFixed(1) : null,
    domNodes: counters.nodes, jsEventListeners: counters.jsEventListeners, idleRaf, ...processMemory(),
    switchP50: pct(.5), switchP95: pct(.95), reloads: loads - loadBaseline, ...snap };
}

const rows = [await measure(0, [])];
console.log(JSON.stringify(rows[0]));
let latencies = [];
for (let c = 1; c <= cycles; c++) {
  for (const href of tabs) { latencies.push(await page.evaluate(h => window.__nav(h), href)); if (pause) await page.waitForTimeout(pause); }
  if (checkpoints.has(c)) { rows.push(await measure(c, latencies)); latencies = []; console.log(JSON.stringify(rows.at(-1))); }
}
console.log('api requests', JSON.stringify(api));
await browser.close();

// Warm-up allocates the pages' first renders; compare the end against cycle 10, not cycle 0.
const warm = rows.find(r => r.cycles === 10) ?? rows[0], last = rows.at(-1), failures = [];
const flat = (key, slack = 0) => { if (typeof last[key] === 'number' && typeof warm[key] === 'number' && last[key] > warm[key] + slack) failures.push(`${key} ${warm[key]} -> ${last[key]}`); };
for (const key of ['domElements', 'svg', 'filters', 'liquidGlass', 'backdrop', 'ro', 'io', 'mo', 'observedDetached', 'urlsLive', 'intervals', 'windowDocListeners', 'exitGhosts', 'reloads']) flat(key, 2);
flat('jsEventListeners', 10); flat('domNodes', 50); flat('wasmMB', 8); flat('jsHeapMB', 5); flat('GPUProcessWsMB', 150);
if (last.idleRaf > 10) failures.push(`an animation frame loop is still running (${last.idleRaf}/s)`);
if (failures.length) { console.error('Did not return to baseline:\n  ' + failures.join('\n  ')); process.exit(1); }
console.log(`Returned to baseline after ${cycles} cycles.`);
