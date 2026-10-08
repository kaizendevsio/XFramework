# Scanner Decoder Provenance

Vendored from the maintained [zxing-wasm 3.1.5 release](https://github.com/Sec-ant/zxing-wasm/releases/tag/v3.1.5)
and its npm package. [Primary API documentation](https://github.com/Sec-ant/zxing-wasm#readme)
covers the full IIFE build, `prepareZXingModule`, `readBarcodes` and `writeBarcode`.
The shared full build is used to decode product symbols and render pairing/product-label QR without
another QR dependency. JavaScript and WASM are served locally; no CDN or camera
frames leave the browser. Preserve the adjacent MIT and Apache-2.0 licenses.

- `dist/iife/full/index.js` -> `zxing-wasm-3.1.5.js`
- `dist/full/zxing_full.wasm` -> `zxing_full.wasm`
- Embedded zxing-cpp commit: `2ecec3f5be0ee803f6e14a5a2c7028c0cfe525b4`
- npm tarball integrity: `sha512-jmxXvTCR/qZxMz2XwR6V+nL2BlDTD1aux7ZWSH9BV77TG9u4k0VyHba/NCCHBHJ60Wg6VV0QL/SMlJczKhJI4w==`

SHA-256:

```text
0BE672F61F2F108B15BF5B04455FDABD4BCA19795690918D5A0953153E78D67B zxing-wasm-3.1.5.js
51B3B1EE268652DD6ACDCDB937D59F42406CA8EFE22DD441ABC01D0B51EDE5B3 zxing_full.wasm
```

When upgrading, obtain both artifacts from the same pinned package, verify its
integrity/licenses, and rerun barcode/video/pairing regressions. No service worker
is installed: authenticated pages or pending codes are not cached for offline use.
