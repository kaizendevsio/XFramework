(() => {
    const api = window.yap.diagnostics, logs = document.getElementById('logs'), notice = document.getElementById('notice');
    const toggle = document.getElementById('logging'); toggle.checked = api.enabled(); logs.value = api.report();
    toggle.onchange = () => { api.setEnabled(toggle.checked); logs.value = api.report(); };
    document.getElementById('copy').onclick = async () => { logs.value = api.report(); const copied = await api.copy(); notice.textContent = copied ? 'Logs copied. You can paste them into your support conversation.' : 'Select and copy the logs below.'; if (!copied) { logs.focus(); logs.select(); } };
    document.getElementById('clear').onclick = () => { api.clear(); logs.value = ''; notice.textContent = 'Saved logs cleared.'; };
})();
