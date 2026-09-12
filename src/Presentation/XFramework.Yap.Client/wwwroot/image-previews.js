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
        jpeg(file) {
            // Avoid decoding several full-resolution photos concurrently on a phone.
            const result = decoding.then(async () => {
                const module = await (decoder ??= import('./vendor/heic-to/heic-to-1.5.2.mjs'));
                return module.heicTo({ blob: file, type: 'image/jpeg', quality: .9 });
            });
            decoding = result.catch(() => {});
            return result;
        }
    };
})();
