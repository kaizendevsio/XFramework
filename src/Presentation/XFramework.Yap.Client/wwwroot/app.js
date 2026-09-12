// Browser preferences and DOM helpers; app state and interactions live in C#.
window.yap = {
    getTheme: () => document.documentElement.dataset.theme,
    setTheme(theme) {
        document.documentElement.dataset.theme = theme;
        document.querySelector('meta[name="theme-color"]').content = theme === 'dark' ? '#181e19' : '#f7f8f2';
        try { localStorage.setItem('yap-theme', theme); } catch {}
    },
    scrollMessages() { const list = document.querySelector('[data-messages]'); if (list) { if (list.classList.contains('message-window')) yap.messageWindow.bottom(list); else list.scrollTop = list.scrollHeight; } },
    showMessage(id) { const list = document.querySelector('.message-window'); if (list) { yap.messageWindow.show(list, id); return; } const element = document.querySelector(`[data-message-id="${CSS.escape(id)}"]`); element?.scrollIntoView({block:'center',behavior:'smooth'}); },
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
        if (element.yapPreviousFocus?.isConnected) element.yapPreviousFocus.focus({preventScroll:true});
    },
    copy: text => navigator.clipboard.writeText(text)
};
let theme;
try { theme = localStorage.getItem('yap-theme'); } catch {}
yap.setTheme(theme === 'dark' || theme === 'light' ? theme : matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');

// Measure overlay chrome without fixed assumptions about composer/header height.
document.addEventListener('DOMContentLoaded', () => {
    const watched = new Set();
    const pinned = new WeakMap();
    const rememberPosition = event => {
        const list = event.target;
        pinned.set(list, list.scrollHeight - list.scrollTop - list.clientHeight < 40);
    };
    const resize = new ResizeObserver(entries => {
        for (const {target} of entries) {
            if (target.hasAttribute('data-messages')) {
                if (target.classList.contains('message-window')) { yap.messageWindow.resize(target); continue; }
                if (pinned.get(target)) target.scrollTop = target.scrollHeight;
                continue;
            }
            const surface = target.closest('[data-scroll-surface]');
            if (surface) {
                surface.style.setProperty(target.hasAttribute('data-scroll-header') ? '--scroll-top' : '--scroll-bottom', `${target.getBoundingClientRect().height}px`);
                const list = surface.querySelector('[data-messages]');
                if (list?.classList.contains('message-window')) yap.messageWindow.resize(list);
                else if (list && pinned.get(list)) list.scrollTop = list.scrollHeight;
            }
        }
    });
    const sync = () => {
        for (const element of watched) if (!element.isConnected) { resize.unobserve(element); element.removeEventListener('scroll', rememberPosition); watched.delete(element); }
        for (const element of document.querySelectorAll('[data-scroll-header],[data-scroll-footer],[data-messages]')) if (!watched.has(element)) {
            watched.add(element);
            if (element.hasAttribute('data-messages')) { pinned.set(element, true); element.addEventListener('scroll', rememberPosition, { passive: true }); }
            resize.observe(element);
        }
    };
    new MutationObserver(sync).observe(document.getElementById('app'), {childList:true,subtree:true});
    sync();
});
