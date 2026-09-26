// Injected before any page script. Counts lifecycle-sensitive browser objects so a
// navigation loop can tell "created and released" apart from "created and kept".
(() => {
  const S = window.__leak = {
    observers: { ResizeObserver: new Set(), IntersectionObserver: new Set(), MutationObserver: new Set() },
    observed: new Map(), // observer -> Set(targets)
    created: { ResizeObserver: 0, IntersectionObserver: 0, MutationObserver: 0 },
    urls: { created: 0, revoked: 0, live: new Set() },
    intervals: new Set(), timeouts: new Set(),
    raf: 0, listenersAdded: 0, listenersRemoved: 0, windowDocListeners: 0,
    viewTransitions: { started: 0, finished: 0 }, canvases: 0, bitmaps: 0, dataUrls: 0,
    longTasks: [], dotnetRefs: new Set(),
  };
  for (const name of Object.keys(S.observers)) {
    const Original = window[name];
    if (!Original) continue;
    const Wrapped = class extends Original {
      constructor(cb, ...rest) { super(cb, ...rest); S.created[name]++; S.observed.set(this, new Set()); }
      observe(target, ...rest) { S.observed.get(this)?.add(target); S.observers[name].add(this); return super.observe(target, ...rest); }
      unobserve(target) { const set = S.observed.get(this); set?.delete(target); if (set && !set.size) S.observers[name].delete(this); return super.unobserve(target); }
      disconnect() { S.observed.get(this)?.clear(); S.observers[name].delete(this); return super.disconnect(); }
    };
    Object.defineProperty(Wrapped, 'name', { value: name });
    window[name] = Wrapped;
  }
  const create = URL.createObjectURL, revoke = URL.revokeObjectURL;
  URL.createObjectURL = function (obj) { const url = create.call(URL, obj); S.urls.created++; S.urls.live.add(url); return url; };
  URL.revokeObjectURL = function (url) { if (S.urls.live.delete(url)) S.urls.revoked++; return revoke.call(URL, url); };
  const si = window.setInterval, ci = window.clearInterval, st = window.setTimeout, ct = window.clearTimeout;
  window.setInterval = function (...a) { const id = si.apply(this, a); S.intervals.add(id); return id; };
  window.clearInterval = function (id) { S.intervals.delete(id); return ci.call(this, id); };
  window.setTimeout = function (fn, ...a) {
    let id; const wrapped = typeof fn === 'function' ? function (...x) { S.timeouts.delete(id); return fn.apply(this, x); } : fn;
    id = st.call(this, wrapped, ...a); S.timeouts.add(id); return id;
  };
  window.clearTimeout = function (id) { S.timeouts.delete(id); return ct.call(this, id); };
  const raf = window.requestAnimationFrame;
  window.requestAnimationFrame = function (cb) { S.raf++; return raf.call(this, cb); };
  const add = EventTarget.prototype.addEventListener, remove = EventTarget.prototype.removeEventListener;
  EventTarget.prototype.addEventListener = function (...a) { S.listenersAdded++; if (this === window || this === document) S.windowDocListeners++; return add.apply(this, a); };
  EventTarget.prototype.removeEventListener = function (...a) { S.listenersRemoved++; if (this === window || this === document) S.windowDocListeners--; return remove.apply(this, a); };
  if (document.startViewTransition) {
    const svt = document.startViewTransition;
    document.startViewTransition = function (...a) {
      const t = svt.apply(this, a); S.viewTransitions.started++;
      t.finished.catch(() => {}).finally(() => S.viewTransitions.finished++); return t;
    };
  }
  const ce = Document.prototype.createElement;
  Document.prototype.createElement = function (tag, ...a) { if (String(tag).toLowerCase() === 'canvas') S.canvases++; return ce.call(this, tag, ...a); };
  const toDataURL = HTMLCanvasElement.prototype.toDataURL;
  HTMLCanvasElement.prototype.toDataURL = function (...a) { S.dataUrls++; return toDataURL.apply(this, a); };
  if (window.createImageBitmap) { const cib = window.createImageBitmap; window.createImageBitmap = function (...a) { S.bitmaps++; return cib.apply(this, a); }; }
  try { new PerformanceObserver(list => { for (const e of list.getEntries()) S.longTasks.push(e.duration); }).observe({ type: 'longtask', buffered: true }); } catch {}

  window.__snapshot = () => {
    const all = document.getElementsByTagName('*');
    let backdrop = 0, backdropHidden = 0;
    for (const el of all) {
      const cs = getComputedStyle(el);
      const bf = cs.backdropFilter || cs.webkitBackdropFilter;
      if (bf && bf !== 'none') { backdrop++; if (!el.getClientRects().length) backdropHidden++; }
    }
    let observedDetached = 0, observedTotal = 0;
    for (const [, set] of S.observed) for (const t of set) { observedTotal++; if (t && t.isConnected === false) observedDetached++; }
    let wasm = null;
    try {
      const rt = globalThis.getDotnetRuntime?.(0);
      wasm = rt?.Module?.HEAP8?.buffer?.byteLength ?? rt?.localHeapViewU8?.().buffer.byteLength ?? null;
    } catch {}
    return {
      path: location.pathname,
      domElements: all.length,
      svg: document.getElementsByTagName('svg').length,
      filters: document.getElementsByTagName('filter').length,
      liquidGlass: document.querySelectorAll('[data-liquid-glass]').length,
      refracted: document.querySelectorAll('[data-refracted]').length,
      backdrop, backdropHidden,
      ro: S.observers.ResizeObserver.size, io: S.observers.IntersectionObserver.size, mo: S.observers.MutationObserver.size,
      roCreated: S.created.ResizeObserver, moCreated: S.created.MutationObserver, ioCreated: S.created.IntersectionObserver,
      observedTotal, observedDetached,
      urlsCreated: S.urls.created, urlsLive: S.urls.live.size,
      intervals: S.intervals.size, timeouts: S.timeouts.size,
      listenersNet: S.listenersAdded - S.listenersRemoved, windowDocListeners: S.windowDocListeners,
      viewTransitions: S.viewTransitions.started, viewTransitionsFinished: S.viewTransitions.finished,
      canvases: S.canvases, dataUrls: S.dataUrls, bitmaps: S.bitmaps,
      exitGhosts: document.querySelectorAll('[data-exit-ghost]').length,
      glassHosts: [...document.querySelectorAll('[data-liquid-glass]')].map(e => e.parentElement?.className).join('|'),
      svgHosts: [...document.getElementsByTagName('svg')].filter(s => !s.closest('.icon, [class*=icon]')).map(s => s.parentElement?.tagName + '.' + (s.parentElement?.className?.baseVal ?? s.parentElement?.className)).join('|'),
      wasm, jsHeapUsed: performance.memory?.usedJSHeapSize ?? null,
      longTasks: S.longTasks.length, longTaskMs: Math.round(S.longTasks.reduce((a, b) => a + b, 0)),
    };
  };
  // rAF requests during an idle second: a runaway loop shows up as ~60/s.
  window.__idleRaf = async (ms = 1000) => { const before = S.raf; await new Promise(r => setTimeout(r, ms)); return S.raf - before; };
  const markers = { '/': '.inbox-panel', '/calls': '.calls-panel', '/saved': '.saved-panel, .body .empty', '/settings': '.body.settings' };
  window.__nav = href => new Promise((resolve, reject) => {
    const link = document.querySelector(`.bottomnav a[href="${href}"]`);
    if (!link) return reject(new Error('no tab link ' + href + ' at ' + location.pathname));
    const started = performance.now();
    link.click();
    const deadline = started + 15000;
    const check = () => {
      if (location.pathname === href && document.querySelector(markers[href]) && document.querySelector(`.bottomnav a[href="${href}"].on`)) {
        raf(() => resolve(performance.now() - started));
      } else if (performance.now() > deadline) reject(new Error('timeout ' + href));
      else raf(check);
    };
    raf(check);
  });
})();
