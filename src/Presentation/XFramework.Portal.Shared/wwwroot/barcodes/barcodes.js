let decoderPromise;

export function decoder() {
    return decoderPromise ??= new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = new URL('./vendor/zxing-wasm-3.1.5.js', import.meta.url).href;
        script.onload = async () => {
            try {
                const library = globalThis.ZXingWASM;
                await library.prepareZXingModule({ overrides: {
                    locateFile: () => new URL('./vendor/zxing_full.wasm', import.meta.url).href
                }, fireImmediately: true });
                resolve(library);
            } catch { decoderPromise = null; reject(new Error('Barcode library could not load. Reload this page.')); }
        };
        script.onerror = () => { decoderPromise = null; script.remove(); reject(new Error('Barcode library could not load. Reload this page.')); };
        document.head.append(script);
    });
}

export async function qrData(value) {
    const library = await decoder();
    const result = await library.writeBarcode(value, { format: 'QRCode', scale: 5 });
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result);
        reader.onerror = () => reject(new Error('QR code could not load.'));
        reader.readAsDataURL(result.image);
    });
}

export async function decode(imageData) {
    const library = await decoder();
    return library.readBarcodes(imageData, {
        formats: ['QRCode', 'EAN13', 'EAN8', 'UPCA', 'UPCE', 'Code128', 'Code39', 'ITF', 'DataMatrix'],
        tryHarder: true, maxNumberOfSymbols: 1
    });
}
