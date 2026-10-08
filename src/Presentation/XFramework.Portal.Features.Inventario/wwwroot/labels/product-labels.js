import { qrData } from '/_content/XFramework.Portal.Shared/barcodes/barcodes.js';
import { createPdf } from '/_content/XFramework.Portal.Shared/reports/report-export.js';
export { qrData };

// Render only the selected authorized catalog snapshot, without making extra queries.
export async function createLabelPdf({ products, copies, width, height, sheet }) {
    if (!Array.isArray(products) || products.length === 0 || !Number.isInteger(copies) || copies < 1 || copies > 100 ||
        products.length * copies > 500 || !Number.isFinite(width) || !Number.isFinite(height) ||
        width < 40 || width > 100 || height < 30 || height > 80 || !['a4', 'label'].includes(sheet) ||
        products.some(p => !p.sku?.trim() || p.sku.length > 256 || /[\x00-\x1f\x7f]/.test(p.sku)))
        throw new Error('Select valid SKUs and up to 500 labels.');
    const a4 = sheet === 'a4';
    const doc = await createPdf({ unit: 'mm', format: a4 ? 'a4' : [width, height],
        orientation: a4 || height >= width ? 'portrait' : 'landscape' });
    const margin = a4 ? 10 : 0, gap = a4 ? 2 : 0;
    const columns = a4 ? Math.floor((190 + gap) / (width + gap)) : 1;
    const rows = a4 ? Math.floor((277 + gap) / (height + gap)) : 1;
    const perPage = columns * rows;
    let index = 0;
    for (const product of products) {
        const source = await qrData(product.sku);
        for (let copy = 0; copy < copies; copy++, index++) {
            if (index > 0 && index % perPage === 0) doc.addPage();
            const slot = index % perPage;
            const x = margin + slot % columns * (width + gap), y = margin + Math.floor(slot / columns) * (height + gap);
            if (a4) { doc.setDrawColor(215); doc.setLineWidth(.15); doc.rect(x, y, width, height); }
            const size = Math.min(height - 6, width * .46);
            doc.addImage(source, 'PNG', x + 3, y + (height - size) / 2, size, size);
            const textX = x + size + 5, textWidth = width - size - 8;
            doc.setFontSize(9); doc.setTextColor(0);
            const lines = doc.splitTextToSize(String(product.name ?? ''), textWidth);
            const maxLines = Math.max(1, Math.floor((height - 16) / 4));
            const name = lines.slice(0, maxLines);
            if (lines.length > maxLines) name[maxLines - 1] = name[maxLines - 1].slice(0, -3) + '...';
            doc.text(name, textX, y + 6);
            doc.setFontSize(7);
            const skuLines = doc.splitTextToSize(product.sku, textWidth);
            const fitted = skuLines.slice(0, 3);
            if (skuLines.length > 3) fitted[2] = fitted[2].slice(0, -3) + '...';
            doc.text(fitted, textX, y + height - 4 - (fitted.length - 1) * doc.getFontSize() * .4);
        }
    }
    return doc;
}

export async function downloadPdf(model) { (await createLabelPdf(model)).save('Product-QR-labels.pdf'); }

export async function printPdf(model) {
    const viewer = window.open('about:blank', '_blank');
    if (!viewer) throw new Error('Allow pop-ups to open the printable PDF.');
    viewer.opener = null;
    try {
        const doc = await createLabelPdf(model);
        doc.autoPrint();
        const url = URL.createObjectURL(doc.output('blob'));
        viewer.location.href = url;
        setTimeout(() => URL.revokeObjectURL(url), 300000);
    } catch (error) { viewer.close(); throw error; }
}
