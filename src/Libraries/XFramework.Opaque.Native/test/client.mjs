// Test-only bridge. Uses the same shipped browser primitives as Yap.
import { createInterface } from 'node:readline';
import * as opaque from '../../../Presentation/XFramework.Yap.Client/wwwroot/vendor/opaque/opaque.mjs';
await opaque.ready;
for await (const line of createInterface({ input: process.stdin })) {
    try { const { method, args } = JSON.parse(line); console.log(JSON.stringify(opaque.client[method]({ ...args, keyStretching: 'memory-constrained' }) ?? null)); }
    catch { console.log(JSON.stringify({ error: 'client protocol failure' })); }
}
