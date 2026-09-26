// In-memory OPFS stand-in for Playwright WebKit on Windows, which ships no OPFS.
// Only for the local benchmark harness: data never leaves the worker's memory.
(() => {
  if (globalThis.navigator?.storage?.getDirectory && globalThis.FileSystemFileHandle?.prototype?.createSyncAccessHandle) return;
  class FileSystemHandle { constructor(kind, name) { this.kind = kind; this.name = name; } async isSameEntry(o) { return o === this; } }
  class FileData { constructor() { this.buf = new Uint8Array(0); this.size = 0; }
    ensure(n) { if (n > this.buf.length) { const b = new Uint8Array(Math.max(n, this.buf.length * 2, 4096)); b.set(this.buf.subarray(0, this.size)); this.buf = b; } } }
  const view = x => x instanceof Uint8Array ? x : ArrayBuffer.isView(x) ? new Uint8Array(x.buffer, x.byteOffset, x.byteLength) : new Uint8Array(x);
  class SyncAccessHandle {
    constructor(d) { this.d = d; this.closed = false; }
    read(buf, opts = {}) { const at = opts.at ?? 0, v = view(buf); const n = Math.max(0, Math.min(v.length, this.d.size - at)); v.set(this.d.buf.subarray(at, at + n)); return n; }
    write(buf, opts = {}) { const at = opts.at ?? 0, v = view(buf); this.d.ensure(at + v.length); this.d.buf.set(v, at); this.d.size = Math.max(this.d.size, at + v.length); return v.length; }
    getSize() { return this.d.size; }
    truncate(n) { this.d.ensure(n); if (n < this.d.size) this.d.buf.fill(0, n, this.d.size); this.d.size = n; }
    flush() {} close() { this.closed = true; }
  }
  class FileSystemFileHandle extends FileSystemHandle {
    constructor(name, d) { super('file', name); this.d = d; }
    async createSyncAccessHandle() { return new SyncAccessHandle(this.d); }
    async getFile() { return new File([this.d.buf.slice(0, this.d.size)], this.name); }
    async createWritable() { const d = this.d; let chunks = []; return { async write(x) { chunks.push(x instanceof Blob ? new Uint8Array(await x.arrayBuffer()) : view(x.data ?? x)); },
      async close() { const total = chunks.reduce((a, c) => a + c.length, 0); d.buf = new Uint8Array(total); let o = 0; for (const c of chunks) { d.buf.set(c, o); o += c.length; } d.size = total; }, async abort() {} }; }
  }
  class FileSystemDirectoryHandle extends FileSystemHandle {
    constructor(name) { super('directory', name); this.children = new Map(); }
    async getDirectoryHandle(name, o = {}) { let c = this.children.get(name); if (!c) { if (!o.create) throw new DOMException('not found', 'NotFoundError'); c = new FileSystemDirectoryHandle(name); this.children.set(name, c); } if (c.kind !== 'directory') throw new DOMException('type', 'TypeMismatchError'); return c; }
    async getFileHandle(name, o = {}) { let c = this.children.get(name); if (!c) { if (!o.create) throw new DOMException('not found', 'NotFoundError'); c = new FileSystemFileHandle(name, new FileData()); this.children.set(name, c); } if (c.kind !== 'file') throw new DOMException('type', 'TypeMismatchError'); return c; }
    async removeEntry(name) { if (!this.children.delete(name)) throw new DOMException('not found', 'NotFoundError'); }
    async resolve() { return null; }
    async *entries() { for (const e of [...this.children]) yield e; }
    async *keys() { for (const k of [...this.children.keys()]) yield k; }
    async *values() { for (const v of [...this.children.values()]) yield v; }
    [Symbol.asyncIterator]() { return this.entries(); }
  }
  const root = new FileSystemDirectoryHandle('');
  Object.assign(globalThis, { FileSystemHandle, FileSystemFileHandle, FileSystemDirectoryHandle, FileSystemSyncAccessHandle: SyncAccessHandle });
  const storage = globalThis.navigator.storage || {};
  try { Object.defineProperty(storage, 'getDirectory', { value: async () => root, configurable: true }); } catch { storage.getDirectory = async () => root; }
  if (!globalThis.navigator.storage) Object.defineProperty(globalThis.navigator, 'storage', { value: storage, configurable: true });
})();
