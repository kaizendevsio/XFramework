# Yap video-call and update recovery review

Reviewed 21 September 2026 against develop `bc816997` (Yap 1.3.57). Changes prepared as 1.3.58.

## Findings and changes

- **Call startup:** a 30-second, single-use socket ticket was requested before loading the SFrame runtime. Slow first-load downloads could consume that lifetime. Crypto now initializes before ticket issuance, without increasing the lifetime or weakening session binding. The deployed server logged a rejected expired connection at 05:00:22 UTC; this is consistent with the risk, but does not prove which failure produced the user's screenshot. Diagnostics now record the safe call phase and exception type, never tickets or keys.
- **Camera restart:** encoder initialization reset the frame counter even though the published video stream survives camera off/on. Receivers reject older IDs. IDs now remain monotonic across encoder restarts.
- **Corrupted/frozen pictures:** dropped complete pictures and decoder backlog did not trigger reference-picture recovery. Reassembly now flags discontinuities; the decoder resets and requests a keyframe before accepting dependent deltas. The bounded sender queue now reports failed writes rather than silently dropping frames, and waits for a new reference picture after a drop.
- **Quality limits:** participant updates could overwrite the device ceiling, and negotiation's low-resolution fallback was not carried into encoder startup. The selected encoder's device/software cap is now retained. Removed the invalid `require-hardware` probe. WebCodecs exposes preference hints, not proof of acceleration; hardware preference remains enabled for actual encoder creation, with conservative codec selection. See [WebCodecs HardwareAcceleration](https://www.w3.org/TR/webcodecs/#enumdef-hardwareacceleration).
- **Update prompt:** installation completion could be observed before `registration.waiting` became visible; a dismissed prompt could also hide every later release in the same page. Track the installed worker and dismiss only that particular update. Check on foreground, online, startup and once per minute, preserving the busy-call/composer guard. Failed checks and redundant installations produce diagnostic breadcrumbs.
- **Interrupted downloads:** installation previously fetched all shell assets in one `cache.addAll`. It now saves integrity-checked batches of four into the new version's isolated cache, allowing retries to reuse verified assets. Installation still fails if any required asset is unavailable. Activation alone removes older shell caches; account storage is untouched.

## Verification

- Blazor client build: passed, with existing SQLite WASM warnings.
- Complete Yap client suite: 242 passed.
- Yap and Bolt browser JavaScript suites: 289 passed.
- Real Chromium module-worker upgrade: v1 activated, v2 downloaded, Update prompt appeared, click activated v2 and reloaded. Only the v2 shell remained; the local-storage sentinel survived. No unregister or site-data reset used.
- Real Chromium H.264 encode/decode: 55 encoded frames, 41 rendered frames at 640x360. An intentionally omitted picture caused one keyframe request and recovery. Camera restart continued frame IDs from 31 to 55 and rendering continued.
- Slow-crypto regression test holds session initialization pending, confirms no socket ticket is requested yet, cancels the call, then verifies a late initialization result cannot create a ticket.

## Remaining limitations

Physical iPhone and Android verification is still required; the exact Android-only failure was not reproduced. The browser tests verify the corrected lifecycle and native H.264 pipeline, not two physical phones over a mobile network.

Codec changes after a group roster change remain a separate gap: the existing published stream retains its original codec configuration. The current UI stops the camera when the negotiated codec changes, but restarting it can reuse that old stream configuration. A proper follow-up should replace the authenticated video stream and decoder together, with relay and group integration tests.

Calls still use a reliable WSS relay. Packet loss on TCP can stall audio and video together, and the per-fragment SFrame/interop path adds overhead. These transport limits are not removed by the decoder fixes; compare a WebRTC media path or benchmark/batch encrypted video work before claiming low-latency parity.

The installed old client must download the release before it receives these update-management changes. Do not advise clearing site data: that can discard local encryption keys and unsent messages.
