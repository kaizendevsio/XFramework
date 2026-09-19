// The in-app emoji keyboard takes the on-screen keyboard's place, so it has to be exactly
// as tall as the keyboard it dismissed or the composer jumps as one replaces the other.
// device.js measures that height while the keyboard is open and publishes --yap-keyboard;
// this restores the last measurement so even the first open of a session lands right.
(() => {
    const recentKey = 'yap-emoji-recent', heightKey = 'yap-keyboard-height', limit = 24;
    const root = document.documentElement;
    if (!root.style.getPropertyValue('--yap-keyboard')) {
        let height = 300; // A phone keyboard, near enough, until this device has been measured.
        try { const saved = Number(localStorage.getItem(heightKey)); if (saved >= 180 && saved <= 500) height = saved; } catch {}
        root.style.setProperty('--yap-keyboard', `${height}px`);
    }
    window.yap.emojiKeyboard = {
        // The attribute lands before the blur so the swap - keyboard out, picker in - is
        // one layout commit: mobile.css keeps the composer's expanded shape either way.
        show(textarea) { root.dataset.emoji = 'open'; textarea?.blur(); },
        hide(textarea, focus) { delete root.dataset.emoji; if (focus) textarea?.focus(); },
        // selectionStart survives a blur, so the caret the person left behind is still theirs.
        caret(textarea) {
            if (!textarea) return [0, 0];
            const start = textarea.selectionStart ?? textarea.value.length;
            return [start, textarea.selectionEnd ?? start];
        },
        place(textarea, index) { try { textarea?.setSelectionRange(index, index); } catch {} },
        recent() {
            try { const saved = JSON.parse(localStorage.getItem(recentKey) || '[]'); return Array.isArray(saved) ? saved.filter(x => typeof x === 'string' && x).slice(0, limit) : []; }
            catch { return []; }
        },
        use(emoji) {
            const next = [emoji, ...this.recent().filter(x => x !== emoji)].slice(0, limit);
            try { localStorage.setItem(recentKey, JSON.stringify(next)); } catch {}
            return next;
        },
        // Arrow keys walk the grid the way a native picker does. Column count is read from
        // the laid-out buttons rather than assumed, so it follows the width at 375px or 1000px.
        grid(element) {
            if (!element || element.dataset.emojiNav) return;
            element.dataset.emojiNav = '1';
            element.addEventListener('keydown', event => {
                if (!['ArrowRight', 'ArrowLeft', 'ArrowUp', 'ArrowDown', 'Home', 'End'].includes(event.key)) return;
                const cells = [...element.querySelectorAll('button')];
                const index = cells.indexOf(document.activeElement);
                if (index < 0) return;
                event.preventDefault();
                const first = cells[0].offsetTop;
                const columns = Math.max(1, cells.findIndex(cell => cell.offsetTop > first) || cells.length);
                const target = { ArrowRight: index + 1, ArrowLeft: index - 1, ArrowDown: index + columns, ArrowUp: index - columns, Home: 0, End: cells.length - 1 }[event.key];
                cells[Math.max(0, Math.min(cells.length - 1, target))]?.focus();
            });
        }
    };
})();
