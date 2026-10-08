let libraries;
let font;

function loadScript(path) {
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = new URL(path, import.meta.url).href;
        script.onload = resolve;
        script.onerror = () => { script.remove(); reject(new Error('PDF export could not load.')); };
        document.head.append(script);
    });
}

async function loadLibraries() {
    libraries ??= (async () => {
        await loadScript('./vendor/jspdf/dist/jspdf.umd.min.js');
        await loadScript('./vendor/autotable/dist/jspdf.plugin.autotable.min.js');
        const response = await fetch(new URL('./vendor/noto/NotoSans-Regular.ttf', import.meta.url));
        if (!response.ok) throw new Error('Report font could not load.');
        const bytes = new Uint8Array(await response.arrayBuffer());
        let binary = '';
        for (let offset = 0; offset < bytes.length; offset += 8192)
            binary += String.fromCharCode(...bytes.subarray(offset, offset + 8192));
        font = btoa(binary);
    })().catch(error => { libraries = null; throw error; });
    await libraries;
}

export async function copyReportLink(url) {
    if (navigator.clipboard?.writeText) {
        try { await navigator.clipboard.writeText(url); return; } catch { }
    }
    const input = document.createElement('textarea');
    input.value = url;
    input.style.position = 'fixed';
    input.style.opacity = '0';
    document.body.append(input);
    input.select();
    try {
        if (!document.execCommand('copy')) throw new Error('Copy is unavailable in this browser.');
    } finally { input.remove(); }
}

// Render the already-authorized report snapshot; exporting never performs another data query.
export async function createPdf(options = { unit: 'mm', format: 'a4' }) {
    await loadLibraries();
    const doc = new window.jspdf.jsPDF(options);
    doc.addFileToVFS('NotoSans-Regular.ttf', font);
    doc.addFont('NotoSans-Regular.ttf', 'NotoSans', 'normal');
    doc.setFont('NotoSans', 'normal');
    return doc;
}

export async function exportPdf(model) {
    const doc = await createPdf();
    const margin = 16;
    const width = 178;
    let y = margin;
    function text(value, size = 10) {
        doc.setFontSize(size);
        const lines = doc.splitTextToSize(String(value ?? ''), width);
        const height = lines.length * size * 0.45;
        if (y + height > 275) { doc.addPage(); y = margin; }
        doc.text(lines, margin, y);
        y += height + 3;
    }
    text(model.title, 18);
    text(model.tenantLabel, 11);
    text(model.scope);
    text(`Generated: ${model.generatedAt}`);
    for (const chart of model.charts ?? []) {
        if (y + 95 > 275) { doc.addPage(); y = margin; }
        text(chart.title, 12);
        const values = chart.values.map(value => Number.isFinite(Number(value)) ? Number(value) : 0);
        const high = Math.max(0, ...values);
        const low = Math.min(0, ...values);
        const range = high - low || 1;
        const plotHeight = 55;
        const baseline = y + high / range * plotHeight;
        const step = width / Math.max(values.length, 1);
        doc.setDrawColor(180);
        doc.line(margin, baseline, margin + width, baseline);
        doc.setFillColor(37, 99, 235);
        values.forEach((value, i) => {
            const height = Math.abs(value) / range * plotHeight;
            doc.rect(margin + i * step + step * .15, value >= 0 ? baseline - height : baseline,
                Math.max(.2, step * .7), height, 'F');
        });
        y += plotHeight + 5;
        const stride = Math.max(1, Math.ceil(values.length / 6));
        doc.setFontSize(7);
        chart.labels.forEach((label, i) => {
            if (i % stride === 0) doc.text(String(label).slice(0, 24), margin + i * step, y);
        });
        y += 8;
        text(`Range: ${low.toLocaleString()} to ${high.toLocaleString()}`, 8);
    }
    for (const section of model.sections ?? []) {
        text(section.heading, 12);
        doc.autoTable({
            startY: y, head: [section.columns], body: section.rows,
            margin: { left: margin, right: margin, top: margin, bottom: 18 },
            styles: { font: 'NotoSans', fontStyle: 'normal', fontSize: 8, cellPadding: 2.5, overflow: 'linebreak' },
            headStyles: { fillColor: [45, 45, 45], fontStyle: 'normal' },
            theme: 'striped',
        });
        y = doc.lastAutoTable.finalY + 9;
    }
    const pages = doc.getNumberOfPages();
    for (let page = 1; page <= pages; page++) {
        doc.setPage(page);
        doc.setFontSize(8);
        doc.setTextColor(100);
        doc.text(`${page} / ${pages}`, 194, 286, { align: 'right' });
    }
    doc.save(`${String(model.title ?? 'Report').replace(/[^a-z0-9_-]/gi, '-')}.pdf`);
}
