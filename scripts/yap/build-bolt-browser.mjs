// Compile the shared Bolt client; checked-in assets keep .NET builds Node-free.
import { execFileSync } from 'node:child_process';
import { copyFileSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
const root = fileURLToPath(new URL('../../', import.meta.url));
execFileSync(process.platform === 'win32' ? 'npx.cmd' : 'npx',
    ['--yes', '--package', 'typescript@5.7.3', 'tsc', '-p', 'src/Libraries/Bolt/Bolt.Browser/tsconfig.json'],
    { cwd: root, stdio: 'inherit', shell: process.platform === 'win32' });
const destination = path.join(root, 'src/Presentation/XFramework.Yap.Client/wwwroot/vendor/bolt');
mkdirSync(destination, { recursive: true });
for (const file of ['bolt-client.js', 'protocol.js', 'media-stream.js'])
    copyFileSync(path.join(root, 'src/Libraries/Bolt/Bolt.Browser/dist', file), path.join(destination, file));
