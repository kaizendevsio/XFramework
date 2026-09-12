// Originals are retained; only the local display copy is converted. The decoder
// is bundled with the offline app and does its HEIF decoding in a web worker.
(() => {
    let decoder;
    let decoding = Promise.resolve();
    const isHeif = (type = '', name = '') => /^image\/hei[cf](?:-sequence)?$/i.test(type.split(';')[0]) || /\.hei[cf]$/i.test(name);
    window.yap.imagePreviews = {
        isHeif,
        async contentType(file) {
            if (isHeif(file.type, file.name)) return 'image/heif';
            if (!file.type || file.type === 'application/octet-stream') {
                const header = new Uint8Array(await file.slice(0, 12).arrayBuffer());
                const brand = String.fromCharCode(...header.slice(8, 12));
                if (['heic', 'heix', 'hevc', 'hevx', 'mif1', 'msf1'].includes(brand)) return 'image/heif';
            }
            return file.type || 'application/octet-stream';
        },
        jpeg(file, heif = true) {
            // Avoid decoding several full-resolution photos concurrently on a phone.
            const result = decoding.then(async () => {
                let bitmap;
                window.yap.diagnostics?.record('image.decode-start', { bytes: file.size });
                try {
                    // Safari can decode HEIF natively. Prefer its decoder to loading
                    // another WASM heap, and resize before keeping a display bitmap.
                    try { bitmap = await createImageBitmap(file, { resizeWidth: 1440 }); }
                    catch (error) {
                        if (!heif) throw error;
                        const module = await (decoder ??= import('./vendor/heic-to/heic-to-1.5.2.mjs'));
                        bitmap = await module.heicTo({ blob: file, type: 'bitmap', options: { resizeWidth: 1440 } });
                    }
                    const scale = Math.min(1, 2048 / Math.max(bitmap.width, bitmap.height));
                    const canvas = document.createElement('canvas');
                    canvas.width = Math.max(1, Math.round(bitmap.width * scale));
                    canvas.height = Math.max(1, Math.round(bitmap.height * scale));
                    try {
                        canvas.getContext('2d').drawImage(bitmap, 0, 0, canvas.width, canvas.height);
                        window.yap.diagnostics?.record('image.decode-complete', { width: canvas.width, height: canvas.height });
                        return await new Promise((resolve, reject) => canvas.toBlob(blob => blob ? resolve(blob) : reject(new Error('Image conversion failed')), 'image/jpeg', .9));
                    } finally { canvas.width = canvas.height = 1; }
                } catch (error) { window.yap.diagnostics?.error('image-decode', error); throw error; }
                finally { bitmap?.close(); }
            });
            decoding = result.catch(() => {});
            return result;
        }
    };
})();
