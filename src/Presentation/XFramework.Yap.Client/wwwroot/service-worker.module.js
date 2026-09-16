// The module service worker: the same worker as service-worker.js, plus the ability to decrypt a
// message on this device and replace the generic banner with who said what.
//
// It re-uses that file rather than restating it, so there is exactly one copy of the offline shell,
// the media caches and the push handling. The imports must stay in this order: each one satisfies a
// guard in the file below it, and a service worker may not use import() at all, so everything the
// push handler can ever need has to be resolved here at startup.
//
// Registered only where module service workers exist (Chrome 91+, Safari 16.4+, Firefox 147+).
// worker-registration.js falls back to the classic worker everywhere else - iOS below 16.4 cannot
// receive a web push at all, so nothing there loses a notification it would otherwise have had.
import './service-worker-assets.js';
import './notifications.js';
import './service-worker.js';
import { preview } from './notification-preview.mjs';

self.yapNotifications.preview = preview;
