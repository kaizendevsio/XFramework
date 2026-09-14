import { encryption } from './encryption.mjs';
import { cryptoRpc } from './crypto-rpc.mjs';

cryptoRpc(self, encryption);
self.postMessage({ ready: true });
