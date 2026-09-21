// Browser preferences and DOM helpers; app state and interactions live in C#.
window.yap = {
    getTheme: () => document.documentElement.dataset.theme,
    applyTheme(theme) {
        document.documentElement.dataset.theme = theme;
        document.querySelector('meta[name="theme-color"]').content = theme === 'dark' ? '#1b1b1b' : '#f7f7f7';
        yap.applyAccent(yap.accentPreference());
    },
    videoPreference() {
        try { const value = JSON.parse(localStorage.getItem('yap-video-quality'));
            if (Array.isArray(value) && value.length === 2) return value; } catch {}
        return [1080, 30];
    },
    setVideoPreference(height, fps) {
        try { localStorage.setItem('yap-video-quality', JSON.stringify([height, fps])); } catch {}
    },
    accentPreference() { try { const value = localStorage.getItem('yap-accent'); return /^#[0-9a-f]{6}$/i.test(value) ? value : '#d5f879'; } catch { return '#d5f879'; } },
    setAccent(value) {
        if (!/^#[0-9a-f]{6}$/i.test(value)) return;
        try { localStorage.setItem('yap-accent', value); } catch {}
        yap.applyAccent(value);
    },
    applyAccent(value) {
        const root = document.documentElement, dark = root.dataset.theme === 'dark';
        const rgb = value.slice(1).match(/../g).map(n => parseInt(n, 16) / 255).map(n => n <= .04045 ? n / 12.92 : ((n + .055) / 1.055) ** 2.4);
        const luminance = rgb[0] * .2126 + rgb[1] * .7152 + rgb[2] * .0722;
        const ink = luminance > .179 ? '#161616' : '#ffffff';
        const tokens = {
            '--accent': value, '--accent2': value, '--on-accent': ink,
            '--accent-ink': `color-mix(in srgb,${value} 55%,${dark ? 'white' : 'black'})`,
            '--accent-soft': `color-mix(in srgb,${value} 18%,var(--surface))`,
            '--bub-out': value, '--bub-out-text': ink
        };
        for (const [key, color] of Object.entries(tokens)) root.style.setProperty(key, color);
    },
    setTheme(theme) { yap.applyTheme(theme); try { localStorage.setItem('yap-theme', theme); } catch {} },
    // "system" is the absence of a stored choice, so it keeps tracking the OS.
    themePreference() { try { return localStorage.getItem('yap-theme') || 'system'; } catch { return 'system'; } },
    setThemePreference(preference) {
        try { preference === 'system' ? localStorage.removeItem('yap-theme') : localStorage.setItem('yap-theme', preference); } catch {}
        yap.applyTheme(preference === 'system' ? (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light') : preference);
    },
    scrollMessages() { const list = document.querySelector('[data-messages]'); if (list) { if (list.classList.contains('message-window')) yap.messageWindow.bottom(list); else list.scrollTop = list.scrollHeight; } },
    showMessage(id) { const list = document.querySelector('.message-window'); if (list) { yap.messageWindow.show(list, id); return; } const element = document.querySelector(`[data-message-id="${CSS.escape(id)}"]`); element?.scrollIntoView({block:'center',behavior:'smooth'}); },
    openSheet(dialog, element) {
        dialog.addEventListener('cancel', event => { event.preventDefault(); element.querySelector('[data-sheet-drag]')?.click(); });
        // Once, as the sheet arrives - not on the renders that follow it.
        yap.haptics?.buzz('tap');
        dialog.showModal(); yap.focusSheet(element);
    },
    // Idempotent: a call re-opens its surface whenever Blazor hands it a different element, and a
    // second listener on the same node would swallow Escape twice.
    openCall(dialog) { if (!dialog.yapCall) { dialog.yapCall = true; dialog.addEventListener('cancel', event => event.preventDefault()); } if (!dialog.open) dialog.showModal(); },
    focusSheet(element) {
        element.yapPreviousFocus = document.activeElement;
        element.yapTrap = event => {
            if (event.key !== 'Tab') return;
            const controls = [...element.querySelectorAll('button:not(:disabled),a[href],input:not(:disabled),textarea:not(:disabled),select:not(:disabled),[tabindex="0"]')].filter(x => x.getClientRects().length);
            const first = controls[0] || element, last = controls.at(-1) || element;
            if (event.shiftKey && (document.activeElement === first || document.activeElement === element)) { event.preventDefault(); last.focus(); }
            else if (!event.shiftKey && (document.activeElement === last || document.activeElement === element)) { event.preventDefault(); first.focus(); }
        };
        element.addEventListener('keydown', element.yapTrap);
        element.focus({preventScroll:true});
    },
    releaseSheet(element) {
        if (!element) return;
        element.removeEventListener('keydown', element.yapTrap);
        const dialog = element.closest('dialog');
        if (dialog?.open) dialog.close();
        if (element.yapPreviousFocus?.isConnected) element.yapPreviousFocus.focus({preventScroll:true});
    },
    copy: text => navigator.clipboard.writeText(text)
};
// Focus rings are a keyboard affordance. Touch and mouse must never paint one,
// including on the programmatically focused sheet container.
const inputModality = mode => { if (document.documentElement.dataset.input !== mode) document.documentElement.dataset.input = mode; };
addEventListener('keydown', event => { if (event.key === 'Tab' || event.key.startsWith('Arrow')) inputModality('keyboard'); }, true);
addEventListener('pointerdown', () => inputModality('pointer'), true);
inputModality('pointer');

let theme;
try { theme = localStorage.getItem('yap-theme'); } catch {}
yap.applyTheme(theme === 'dark' || theme === 'light' ? theme : matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
matchMedia('(prefers-color-scheme: dark)').addEventListener('change', event => {
    if (yap.themePreference() === 'system') yap.applyTheme(event.matches ? 'dark' : 'light');
});

// Measure overlay chrome without fixed assumptions about composer/header height.
document.addEventListener('DOMContentLoaded', () => {
    const watched = new Set();
    let headerHeight = null;
    const publishHeader = height => {
        if (headerHeight === height) return;
        headerHeight = height;
        requestAnimationFrame(() => document.documentElement.style.setProperty('--yap-header', `${headerHeight}px`));
    };
    const pinned = new WeakMap();
    const rememberPosition = event => {
        const list = event.target;
        pinned.set(list, list.scrollHeight - list.scrollTop - list.clientHeight < 40);
    };
    const resize = new ResizeObserver(entries => {
        for (const {target} of entries) {
            if (target.hasAttribute('data-messages')) {
                if (target.classList.contains('message-window')) continue;
                if (pinned.get(target)) target.scrollTop = target.scrollHeight;
                continue;
            }
            const surface = target.closest('[data-scroll-surface]');
            if (surface && target.getClientRects().length) {
                const height = target.getBoundingClientRect().height;
                const header = target.hasAttribute('data-scroll-header');
                surface.style.setProperty(header ? '--scroll-top' : '--scroll-bottom', `${height}px`);
                // Toasts live outside the surface. Publish its header height in a
                // frame to avoid a ResizeObserver loop when writing to the document.
                if (header) publishHeader(height);
                const list = surface.querySelector('[data-messages]');
                if (list?.classList.contains('message-window')) yap.messageWindow.resize(list);
                else if (list && pinned.get(list)) list.scrollTop = list.scrollHeight;
            }
        }
    });
    const sync = () => {
        if (!document.querySelector('[data-scroll-header]')) publishHeader(0);
        for (const element of watched) if (!element.isConnected) { resize.unobserve(element); element.removeEventListener('scroll', rememberPosition); watched.delete(element); }
        for (const element of document.querySelectorAll('[data-scroll-header],[data-scroll-footer],[data-messages]')) if (!watched.has(element)) {
            watched.add(element);
            if (element.hasAttribute('data-messages') && !element.classList.contains('message-window')) { pinned.set(element, true); element.addEventListener('scroll', rememberPosition, { passive: true }); }
            resize.observe(element);
        }
    };
    new MutationObserver(sync).observe(document.getElementById('app'), {childList:true,subtree:true});
    sync();
});
