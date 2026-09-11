// Browser preferences and DOM helpers; app state and interactions live in C#.
window.yap = {
    deviceId() {
        try { let id = sessionStorage.getItem('yap-device'); if (!id) { id = crypto.randomUUID(); sessionStorage.setItem('yap-device', id); } return id; }
        catch { return crypto.randomUUID(); }
    },
    isVisible: () => document.visibilityState === 'visible' && document.hasFocus(),
    nearBottom() { const list = document.querySelector('[data-messages]')?.closest('.scroll-content'); return !!list && list.scrollHeight - list.scrollTop - list.clientHeight < 100; },
    getTheme: () => document.documentElement.dataset.theme,
    setTheme(theme) {
        document.documentElement.dataset.theme = theme;
        document.documentElement.classList.toggle('dark', theme === 'dark');
        document.querySelector('meta[name="theme-color"]').content = theme === 'dark' ? '#181e19' : '#f7f8f2';
        try { localStorage.setItem('yap-theme', theme); } catch {}
    },
    scrollMessages() { const list = document.querySelector('[data-messages]')?.closest('.scroll-content'); if (list) list.scrollTop = list.scrollHeight; },
    focusMessage(id) { const message = document.getElementById(`message-${id}`); if (message) { message.scrollIntoView({block:'center'}); message.focus({preventScroll:true}); } },
    focusSheet: element => element.focus({preventScroll:true}),
    copy: text => navigator.clipboard.writeText(text)
};
let theme;
try { theme = localStorage.getItem('yap-theme'); } catch {}
yap.setTheme(theme === 'dark' || theme === 'light' ? theme : matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');

// Measure overlay chrome without fixed assumptions about composer/header height.
document.addEventListener('DOMContentLoaded', () => {
    const watched = new Set();
    const resize = new ResizeObserver(entries => {
        for (const {target} of entries) {
            const surface = target.closest('[data-scroll-surface]');
            if (surface) surface.style.setProperty(target.hasAttribute('data-scroll-header') ? '--scroll-top' : '--scroll-bottom', `${target.getBoundingClientRect().height}px`);
        }
    });
    const sync = () => {
        for (const element of watched) if (!element.isConnected) { resize.unobserve(element); watched.delete(element); }
        for (const element of document.querySelectorAll('[data-scroll-header],[data-scroll-footer]')) if (!watched.has(element)) { watched.add(element); resize.observe(element); }
    };
    new MutationObserver(sync).observe(document.getElementById('app'), {childList:true,subtree:true});
    sync();
});
