# Yap installability and mobile zoom verification

Date: 2026-09-11.

## Scope

- Online PWA manifest with stable root identity/start URL, standalone display, 192px/512px PNG icons, and a 180px Apple touch icon. Icons use the existing Yap/Lucide chat mark, with opaque padding for maskable launchers; `wwwroot/icon.svg` is the vector source.
- Installation instructions on login, registration, and Settings, hidden by the standalone display-mode media query.
- Fixed viewport zoom, scrolling-only touch-action rules including nested scrollers, and non-passive Safari gesture/multi-touch cancellation. Single-finger events are not canceled. Existing 16px mobile form/composer sizing is retained.
- No service worker, offline messaging, push notifications, or client-side cache of private account/chat content. **Superseded:** see the 2026-09-15 addendum at the end of this file; all four now exist.

## Local validation

- `dotnet test src/Tests/Yap.Tests/Yap.Tests.csproj -m:1 /nr:false`: **40 passed**.
- New HTTP-host test verifies public manifest MIME type, icon MIME types and actual PNG dimensions, login/registration manifest links, and authenticated protection of the manifest start URL.
- Chromium mobile emulation: iPhone 12 (390px) and Pixel 7 (412px). `Page.getAppManifest` and `Page.getInstallabilityErrors` both report no errors.
- Native synthesized pinch gestures leave viewport scale at **1 to 1** on both device profiles. Explicit single-finger touch drags scroll the registration surface (0 to 81px on iPhone, 0 to 184px on Pixel with install help expanded).
- Safari gesture event handlers cancel gesturestart/change/end; two-finger touchmove is canceled, one-finger touchmove is not.
- Registration fields and chat composer compute to 16px. Document width matches both emulated viewports, without horizontal overflow.
- Fixture login, conversation opening, composing, and sending a message succeed. The automation tool's click simulation did not activate Blazor commands reliably, so DOM activation was used for conversation selection/send; this is not evidence of physical-device tap behavior.
- Inspected mobile registration and icon screenshots. Installation help expands without navigating away or submitting the form.

## Limits and references

Physical iOS Safari/Android home-screen installation and OS gesture behavior cannot be verified from this Windows environment. Chromium iPhone emulation is not WebKit. OS magnification and browser accessibility overrides are outside the application's control. Standalone launch is declared in the manifest; actual phone installation still requires a device check.

Current installation requirements: [MDN: Making PWAs installable](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps/Guides/Making_PWAs_installable). iPhone installation steps: [Apple: Turn a website into an app](https://support.apple.com/en-euro/guide/iphone/iphea86e5236/ios). Safari gesture cancellation: [Apple: Handling Events](https://developer.apple.com/library/archive/documentation/AppleApplications/Reference/SafariWebContent/HandlingEvents/HandlingEvents.html).

## Addendum: web push, 2026-09-15

The scope line above describes the 2026-09-11 build and is no longer accurate. Since then the app gained a service worker, an offline store, and now native Web Push.

- `wwwroot/service-worker.js` and `wwwroot/service-worker.published.js` both handle `push` and `notificationclick`. `src/Tests/Yap.Tests/service-worker.test.mjs` runs the same push suite against both files (21 assertions across install/activate plus push behaviour): generic banner content, call collapse keys, empty and unreadable payloads, visible-window suppression, focus-and-navigate on tap, and cross-origin window rejection.
- Payload encryption is verified against the RFC 8291 section 5 vector in `Notifications.Tests`, including every documented intermediate value (ECDH secret, PRK_key, IKM, PRK, CEK, NONCE) and the final ciphertext.
- Not verified here: delivery on a physical iOS 16.4+ home-screen install or an Android Chrome install. That needs a real device, real VAPID keys, and a public HTTPS origin; no VAPID key pair is configured in any environment yet, so no end-to-end push has been observed.
- The manifest already satisfies Apple's requirements (`display: standalone`, `scope: /`, HTTPS, root-scope service worker registration); the earlier installability checks in this report still hold.
