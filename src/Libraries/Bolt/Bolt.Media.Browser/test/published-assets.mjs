// Run after publishing the consuming app:
// node src/Libraries/Bolt/Bolt.Media.Browser/test/published-assets.mjs <published-wwwroot>
import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const library = fileURLToPath(new URL('../', import.meta.url));
assert.ok(process.argv[2], 'Pass the consuming app published wwwroot directory.');
const published = resolve(process.argv[2]);
const imports = new Set();
for (const filename of readdirSync(library).filter(name => name.endsWith('.cs'))) {
    const source = readFileSync(resolve(library, filename), 'utf8');
    for (const match of source.matchAll(/"\.\/(_content\/[^"\r\n]+\.js)"/g)) imports.add(match[1]);
}
assert.ok(imports.size >= 2, 'Expected audio/video and crypto module imports.');
for (const relativePath of imports) {
    const asset = resolve(published, relativePath);
    assert.ok(statSync(asset).size > 0, `Missing published JS import: ${relativePath}`);
    const source = readFileSync(asset, 'utf8');
    for (const match of source.matchAll(/new URL\('\.\/([^']+\.js)', import.meta.url\)/g))
        assert.ok(statSync(resolve(dirname(asset), match[1])).size > 0, `Missing published audio worklet: ${match[1]}`);
}
console.log(`Verified ${imports.size} published Bolt Media module paths and referenced worklets.`);
