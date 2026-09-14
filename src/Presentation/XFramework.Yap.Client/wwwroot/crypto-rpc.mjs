// MessagePort RPC with pull-based byte streams. Uses no transferable-stream API
// (not available in every mobile browser) and never buffers a whole attachment.
export function cryptoRpc(port, methods = {}) {
    let next = 0, closed = false;
    const pending = new Map(), resources = new Set();
    const fail = reason => {
        if (closed) return;
        closed = true;
        const error = reason instanceof Error ? reason : new Error('Encryption worker stopped. Retry the operation.');
        for (const job of pending.values()) job.reject(error);
        pending.clear();
        for (const dispose of [...resources]) dispose(error);
        resources.clear();
        port.close?.();
        api.onClose?.(error);
    };
    function child(methods) {
        const channel = new MessageChannel();
        const rpc = cryptoRpc(channel.port1, methods);
        const dispose = error => rpc.close(error);
        resources.add(dispose);
        return { port: channel.port2, rpc, dispose: () => { resources.delete(dispose); rpc.close(); } };
    }
    function pack(value, transfers, owners = []) {
        if (value instanceof ReadableStream) {
            const reader = value.getReader();
            const stream = child({
                async pull() {
                    try { return await reader.read(); }
                    catch (error) { reader.releaseLock(); throw error; }
                },
                async cancel() { try { await reader.cancel(); } finally { reader.releaseLock(); } },
                finish() { reader.releaseLock(); }
            });
            // Receiver closes the channel after consuming EOF or cancelling.
            stream.rpc.onRelease = stream.dispose;
            stream.rpc.onClose = () => { reader.cancel().catch(() => {}).finally(() => { try { reader.releaseLock(); } catch {} }); };
            owners.push(stream.dispose);
            transfers.push(stream.port);
            return { yapCryptoStream: stream.port };
        }
        if (value && typeof value.write === 'function' && typeof value.commit === 'function' && typeof value.abort === 'function') {
            let finished = false;
            const sink = child({ write: bytes => value.write(bytes),
                async commit() { await value.commit(); finished = true; },
                async abort() { try { await value.abort(); } finally { finished = true; } } });
            sink.rpc.onRelease = sink.dispose;
            sink.rpc.onClose = () => { if (!finished) Promise.resolve().then(() => value.abort()).catch(() => {}); };
            owners.push(sink.dispose);
            transfers.push(sink.port);
            return { yapCryptoSink: sink.port };
        }
        if (Array.isArray(value)) return value.map(item => pack(item, transfers, owners));
        if (value && Object.getPrototypeOf(value) === Object.prototype)
            return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, pack(item, transfers, owners)]));
        return value;
    }
    function unpack(value, owners = []) {
        if (value?.yapCryptoStream || value?.yapCryptoSink) {
            const rpc = cryptoRpc(value.yapCryptoStream ?? value.yapCryptoSink);
            const dispose = error => rpc.close(error);
            resources.add(dispose);
            const release = () => { resources.delete(dispose); rpc.release(); };
            owners.push(release);
            if (value.yapCryptoSink) return {
                write: bytes => rpc.call('write', [bytes]),
                async commit() { try { await rpc.call('commit', []); } finally { release(); } },
                async abort() { try { await rpc.call('abort', []); } finally { release(); } }
            };
            return new ReadableStream({
                async pull(controller) {
                    try {
                        const result = await rpc.call('pull', []);
                        if (result.done) { await rpc.call('finish', []); release(); controller.close(); }
                        else controller.enqueue(result.value);
                    } catch (error) { release(); controller.error(error); }
                },
                async cancel() { try { await rpc.call('cancel', []); } finally { release(); } }
            }, { highWaterMark: 0 });
        }
        if (Array.isArray(value)) return value.map(item => unpack(item, owners));
        if (value && Object.getPrototypeOf(value) === Object.prototype)
            return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, unpack(item, owners)]));
        return value;
    }
    const api = {
        close: fail,
        release() { if (!closed) { port.postMessage({ release: true }); fail(); } },
        call(method, args) {
            if (closed) return Promise.reject(new Error('Encryption worker stopped. Retry the operation.'));
            return new Promise((resolve, reject) => {
                const id = ++next;
                const owners = [];
                pending.set(id, { resolve, reject, owners });
                try { const transfers = []; port.postMessage({ id, method, args: pack(args, transfers, owners) }, transfers); }
                catch (error) { pending.delete(id); owners.forEach(dispose => dispose()); reject(error); }
            });
        }
    };
    port.onmessage = async ({ data }) => {
        if (data.release) { api.onRelease?.(); return; }
        if (data.method) {
            const owners = [];
            try {
                if (!Object.hasOwn(methods, data.method) || typeof methods[data.method] !== 'function') throw new Error('Unsupported encryption operation.');
                const result = await methods[data.method](...unpack(data.args, owners));
                const transfers = [];
                port.postMessage({ id: data.id, result: pack(result, transfers) }, transfers);
            } catch (error) { owners.forEach(dispose => dispose()); if (!closed) port.postMessage({ id: data.id, error: { name: error.name, message: error.message } }); }
        } else {
            const job = pending.get(data.id);
            if (!job) return;
            pending.delete(data.id);
            if (data.error) { job.owners.forEach(dispose => dispose()); job.reject(Object.assign(new Error(data.error.message), { name: data.error.name })); }
            else job.resolve(unpack(data.result));
        }
    };
    port.onmessageerror = () => fail();
    port.start?.();
    return api;
}
