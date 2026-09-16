import { encryption } from './encryption.mjs';
import { passwordRecovery } from './password-recovery.mjs';
import { cryptoRpc } from './crypto-rpc.mjs';

cryptoRpc(self, { ...encryption, ...passwordRecovery(encryption) });
self.postMessage({ ready: true });
