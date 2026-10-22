// Print an isolated document: the Portal shell and sale audit never enter the print tree.
export async function createReceiptFrame(html, widthMm) {
    if (widthMm !== 58 && widthMm !== 80) throw new Error("Unsupported receipt width.");
    const frame = document.createElement("iframe");
    frame.title = "Printable receipt";
    frame.setAttribute("sandbox", "allow-same-origin allow-modals");
    frame.style.cssText = `position:fixed;left:-10000px;top:0;width:${widthMm}mm;height:100px;border:0;`;
    const loaded = new Promise((resolve, reject) => {
        const timeout = setTimeout(() => reject(new Error("Receipt loading timed out.")), 15000);
        frame.onload = () => { clearTimeout(timeout); resolve(); };
    });
    frame.srcdoc = html;
    document.body.append(frame);
    try {
        await loaded;
        const doc = frame.contentDocument;
        await doc.fonts.ready;
        await Promise.all([...doc.images].map(img => img.decode()));
        // CSS paged media requires two dimensions. Measure the roll after fonts/logo finish loading.
        const heightMm = Math.max(60, Math.ceil(doc.querySelector(".receipt").getBoundingClientRect().height * 25.4 / 96) + 2);
        const style = doc.createElement("style");
        style.textContent = `@page{size:${widthMm}mm ${heightMm}mm;margin:0}`;
        doc.head.append(style);
        frame.style.height = `${heightMm}mm`;
        return frame;
    } catch (error) {
        frame.remove();
        throw error;
    }
}

export async function printReceipt(html, widthMm) {
    const frame = await createReceiptFrame(html, widthMm);
    const cleanup = () => frame.remove();
    frame.contentWindow.addEventListener("afterprint", cleanup, { once: true });
    // Some browser print dialogs do not emit afterprint when cancelled.
    setTimeout(cleanup, 300000);
    try {
        frame.contentWindow.focus();
        frame.contentWindow.print();
    } catch (error) {
        cleanup();
        throw error;
    }
}
