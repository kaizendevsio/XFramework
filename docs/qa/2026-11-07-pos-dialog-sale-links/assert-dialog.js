(() => {
    const dialog = document.querySelector('[role="dialog"].pos-scroll-dialog');
    if (!dialog) throw new Error('Expected an open POS scroll dialog');
    const body = dialog.querySelector('.pos-scroll-dialog-body');
    const rect = dialog.getBoundingClientRect();
    const buttons = [...dialog.querySelectorAll('button')].filter(button =>
        ['Cancel', 'Create Return', 'Close preview', 'Print receipt', 'Close'].includes(button.textContent.trim()) ||
        button.getAttribute('aria-label') === 'Close');
    const fits = r => r.top >= 0 && r.left >= 0 && r.bottom <= innerHeight && r.right <= innerWidth;
    if (!fits(rect)) throw new Error('Dialog exceeds viewport: ' + JSON.stringify(rect));
    if (!body || getComputedStyle(body).overflowY !== 'auto') throw new Error('Body must scroll independently');
    if (buttons.length < 2) throw new Error('Missing dialog actions');
    const actions = buttons.map(button => {
        const bounds = button.getBoundingClientRect();
        if (!fits(bounds)) throw new Error('Action is clipped: ' + button.textContent);
        const hit = document.elementFromPoint(bounds.x + bounds.width / 2, bounds.y + bounds.height / 2);
        if (!button.contains(hit)) throw new Error('Action is obscured: ' + button.textContent);
        return { text: button.textContent.trim(), disabled: button.disabled, bounds: bounds.toJSON() };
    });
    return { viewport: { width: innerWidth, height: innerHeight }, dialog: rect.toJSON(),
        body: { height: body.clientHeight, scrollHeight: body.scrollHeight }, actions };
})();
