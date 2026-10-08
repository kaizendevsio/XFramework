import { RecoveryBuffer } from './recovery-buffer.mjs';
import { readFileSync } from 'node:fs';
const fixture = JSON.parse(readFileSync(0, 'utf8'));
const buffer = new RecoveryBuffer(fixture.recover, fixture.keyframeSupersedes);
buffer.configure(fixture.rtt);
const results = [];
for (const op of fixture.ops) {
    const ready = [], nacks = [];
    if (op.type === 'push') buffer.push(op.sequence, new Uint8Array(Buffer.from(op.fragment, 'base64')), op.at, ready);
    else if (op.type === 'poll') buffer.poll(op.at, ready, nacks);
    else if (op.type === 'decline') buffer.decline(op.sequences, ready);
    results.push({ ready: ready.map(p => ({ ...p, data: Buffer.from(p.data).toString('base64') })),
        nacks: nacks.sort((a, b) => a - b), stats: { ...buffer.stats }, window: buffer.window });
}
process.stdout.write(JSON.stringify(results));
