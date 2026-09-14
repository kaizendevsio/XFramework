// Manual encrypted WASM benchmark. Start the --serve fixture with
// YAP_FIXTURE_ENCRYPTION=1, sign in as fixture/fixture in a fresh browser,
// open Encrypted browser fixture, then evaluate this file in that page.
// Adds 150 ms per API response and attempts 12 messages at 500 ms intervals.
// Measures local queuing, focus retention, server receipts and browser long tasks.
// Desktop emulation does not establish physical iOS keyboard behavior.
(async () => {
    if (location.hostname !== '127.0.0.1' || !document.querySelector('h1')?.textContent.includes('Encrypted browser fixture'))
        throw new Error('Run only in the owned local encrypted browser fixture.');
    const editor = document.querySelector('.composer textarea');
    if (!editor) throw new Error('Open the conversation before benchmarking.');
    const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
    const stats = { attempts: 12, accepted: 0, disabledAttempts: 0, focusLosses: 0, posts: [], longTasks: [], queuedMs: [] };
    const prefix = `Benchmark ${Date.now()} `;
    const started = performance.now();
    const original = window.fetch;
    const observer = new PerformanceObserver(list => stats.longTasks.push(...list.getEntries().map(x => Math.round(x.duration))));
    observer.observe({ type: 'longtask' });
    window.fetch = async (...args) => {
        const response = await original(...args);
        const path = new URL(response.url).pathname;
        if (path.startsWith('/api/')) await sleep(150);
        if (path === '/api/chat/messages' && response.ok) stats.posts.push(Math.round(performance.now() - started));
        return response;
    };
    const queued = [];
    try {
        editor.focus();
        for (let i = 0; i < stats.attempts; i++) {
            await sleep(Math.max(0, started + i * 500 - performance.now()));
            if (editor.disabled) { stats.disabledAttempts++; continue; }
            editor.focus();
            const text = prefix + i;
            editor.value = text;
            editor.dispatchEvent(new Event('input', { bubbles: true }));
            await sleep(35);
            const send = document.querySelector('.sendbtn');
            if (send.disabled) { stats.disabledAttempts++; continue; }
            const sent = performance.now();
            send.click(); stats.accepted++;
            await sleep(35);
            if (document.activeElement !== editor) stats.focusLosses++;
            queued.push((async () => {
                while (performance.now() - sent < 15000) {
                    if ([...document.querySelectorAll('.msg.out .bub')].some(x => x.innerText.trim() === text)) {
                        stats.queuedMs.push(Math.round(performance.now() - sent)); return;
                    }
                    await sleep(20);
                }
            })());
        }
        while (stats.posts.length < stats.accepted && performance.now() - started < 30000) await sleep(50);
        await Promise.all(queued);
        return { ...stats, elapsedMs: Math.round(performance.now() - started) };
    } finally {
        window.fetch = original;
        observer.disconnect();
    }
})()
