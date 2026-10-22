# Running the native video benchmark

## Scope

This is a separate, video-only two-phone prototype, not a replacement for Yap calling. Both hosts show the existing HTTPS web application. Open `/diagnostics/native-media` in that application after deploying the client page from this branch. Native views display video above the web controls. No video buffers cross the WebView bridge.

The prototype uses H.264, native hardware-required encoding, and an encrypted, explicitly paired LAN TCP connection. It deliberately excludes normal Yap call admission, audio, Bolt SFrame, peer ICE, TURN, congestion adaptation and group calls. Do **not** compare transport performance directly with the existing Bolt UDP path. This first experiment answers whether the native video path materially improves sustained capture/display and heat. It does not justify a production migration by itself.

## Android

Run `scripts/yap/build-native-android.ps1`. The signed Release APK is under `src/Presentation/XFramework.Yap.Native.Android/bin/Release/net10.0-android/android-arm64/publish/`. This is development signing, not Play Store distribution. Install the `-Signed.apk` with `adb install -r <path>` or transfer it to your phone and allow installation from that source. The application label is **Yap native benchmark**, separate from the existing PWA.

## iPhone

Use .NET 10's iOS workload compatible with installed Xcode. On the supplied Mac, SDK 10.0.102 / iOS 26.2.10191 matches Xcode 26.2. `bash scripts/yap/build-native-ios.sh --unsigned` validates a device build but does not produce an installable signed app.

Do not use existing employer/organization profiles. In Xcode Settings → Accounts, sign in with your personal Apple Account. Create a temporary iOS App project with bundle identifier `io.kaizendevs.yap.nativebenchmark`, choose your **Personal Team**, enable automatic signing, select the paired iPhone and build once. This creates a development identity/profile. Apple may require a unique bundle ID; if it does, set the same `ApplicationId` on the .NET build. A free Personal Team profile expires after seven days and must be renewed/reinstalled. No App Store review is required for this development installation.

Then set `YAP_CODESIGN_KEY` to that Apple Development identity and `YAP_CODESIGN_PROVISION` to the new profile's UUID/name; run `bash scripts/yap/build-native-ios.sh`. If the default bundle ID is already registered to another team, create a unique identifier and set `YAP_APPLICATION_ID` to the same value (the current personal installation uses `io.kaizendevs.yap.nativebenchmark.personal`). Keep passwords and signing keys out of the repository. Install the signed `.app` with Xcode Devices or `xcrun devicectl device install app --device <device-id> <app-path>`. Enable Developer Mode/trust if iOS requests it.

Apple: [personal device testing](https://developer.apple.com/help/account/basics/about-your-developer-account), [device signing requirements](https://help.apple.com/xcode/mac/current/en.lproj/dev5a825a1ca.html).

## Pair and run

1. Put both phones on the same non-isolated Wi-Fi. Allow camera/local-network permission. Open the prototype and enter the HTTPS Yap origin in its top address field if the default address is not reachable. The page must exist on that origin; the APK does not update the server.
2. End regular Yap calls. Select 1080p30 and 6 Mbps on **both** phones. Use the same camera as the web comparison. The setting is per sender, not copied by the invitation.
3. On the host, enter its Wi-Fi IPv4 address from phone Wi-Fi settings; press **Create paired test**. Copy the temporary invitation to the second phone and press **Join paired test**. The invite pins the TLS certificate and authenticates the peer. Do not put it in benchmark reports or public chats.
4. Both native cameras start when pairing completes. Stop cancels the run on both ends. Navigating, backgrounding or disconnecting stops capture. Pairing waits at most three minutes; a run is capped at fifteen minutes. Re-pair after stopping or changing camera/FPS.
5. Test front/rear cameras separately, then rotate portrait → landscape → portrait. Record whether either image is cropped, mirrored unexpectedly or sideways. These orientation cases require physical verification on both platforms.
6. Cool devices before measurements. Run 12 minutes at 1080p30 (discard first two), followed by an equivalent web call with both microphones muted, same brightness/scene/bitrate and network where possible. Repeat in reverse order. Only then test 1080p60. If a camera/codec rejects 60 FPS, record the rejection rather than substituting 30 FPS.
7. Stop and press **Export numeric report**. iOS uses its share sheet; Android copies numeric JSONL to the clipboard. Reports are also stored in the app's private `benchmarks/` directory. Never enable camera/microphone data logging.

### Pairing troubleshooting

If iPhone reports **connection refused**, check the Android host status before changing IP addresses. The original Android build stopped its listener on every activity pause, including switching apps to transfer the invitation. The corrected Android build preserves a pending invitation during its existing three-minute window. Return Android to the benchmark before joining from iPhone; an active video test still stops when backgrounded. Create a fresh invitation after a stop, timeout, or app reinstall. Only the host needs its own Wi-Fi address. The iPhone client uses the address and port inside the invitation.

The Android lifecycle fix has shared TLS integration coverage for connecting before and after returning to the foreground, and verifies that active video resources are disposed on backgrounding. This does not yet change the iOS host's background behavior; use Android as the host for this comparison.

Device verification on the Galaxy S25 Ultra confirmed the fresh pairing TCP listener remained open after pressing Home and returning to the app. All twelve shared tests passed, and the corrected Release APK was installed through ADB. The full iPhone-to-Android video run still needs verification.

The subsequent iPhone `Argument_InvalidHostName` failure came from using the display label `Yap native benchmark` as the TLS client's `TargetHost`. It now uses the invitation's IPv4 address. The exact certificate SHA-256 pin and pairing-secret verification remain unchanged. This fix requires updating the native iPhone app; refreshing the web page alone cannot apply it.

The next device test stalled before TLS: an iPhone header-only RVI trace showed repeated TCP SYNs from `192.168.50.10` to the current Android invitation port, with no SYN-ACK. Android was still listening; the Mac (`192.168.50.79`) connected to the same listener in about 20 ms. Retrying with only the iPhone VPN disabled still showed unanswered SYNs to a new invitation port. This narrows the failure to the phone-to-phone TCP path but does not establish which device/network rule drops the traffic. A separate retry of an already-consumed port received TCP resets, so distinguish expired invitations from silent connection timeouts.

Pairing now reports TCP connection, TLS negotiation, secret verification and camera startup separately. Its internal connection/handshake deadlines produce descriptive timeout errors, while explicit user/lifecycle cancellation remains `Stopped`. Fourteen shared tests pass, including a real stalled TLS peer and explicit cancellation. No certificate verification was removed, and no claim of successful two-phone video follows from these transport tests.

### Device startup verification (2026-10-23)

With both phone VPNs disabled, the user confirmed that pairing connected. Inspection of both screens then identified Android `Arg_InvalidCastException` and iPhone `IO_EOF_ReadBeyondEOF`. Android was casting Camera2's Java array of FPS ranges directly to a managed array; it now marshals the elements through `ToArray<Android.Util.Range>()`. The iPhone EOF was consistent with the Android peer closing during camera startup.

The corrected APK was installed on the Galaxy S25 Ultra. A 12-second physical-device smoke test sent 334 H.264 pictures (10,400,775 bytes) through the pinned TLS connection to a Mac echo peer and back into Android's native decoder. A late sample reported approximately 30 capture / 29 encoded FPS, zero queue drops, and hardware `c2.qti.avc.encoder` / `c2.qti.avc.decoder`. No media was saved. This verifies Android startup and its encode/decode path, not iPhone interoperability, sustained heating, or production Bolt performance.

For developer-controlled pairing, run `bash scripts/yap/join-native-ios.sh <device-id> <private-invitation-file>` on the paired Mac. The iPhone prototype accepts a one-shot `YAP_BENCHMARK_INVITATION` process environment variable supplied by `devicectl device process launch`. After trusted-page navigation, it waits up to a minute for the page's enabled Join action, fills the invitation and invokes that action. This preserves the regular-call guard, status listener and Stop button state. The fresh page defaults to 1080p30 / 6 Mbps / front camera. Ordinary app launches remain manual. Use a fresh Android invitation, keep it out of logs, and remove temporary invitation files after launch. Certificate pinning and secret validation are unchanged. iOS writes status/error notices (no invitations or media) to its private `Documents/native-status.log`, reset on each launch, for device-side startup diagnosis.

The automated two-phone retry reached `Native video running` on iPhone, then stopped with `Arg_PlatformNotSupported`. The sampler used `System.Diagnostics.Process` CPU/memory APIs, which are unsupported on iOS. It now skips that optional process probe on iOS and reports null CPU/memory fields; capture, encode, receive, output and thermal sampling remain enabled. Android retains its supported process metrics. Missing metrics must not be interpreted as zero CPU/memory usage. All fourteen shared tests pass after this change; physical verification is recorded below when complete.

After installing the corrected iPhone build, automated pairing produced video on both physical devices. At approximately 60 seconds, both reported about 30 capture/encode/receive/output callbacks per second at 1920x1080, front cameras, requested 6 Mbps. Android reported zero queue drops and hardware Qualcomm encode/decode; iPhone reported nominal thermal state, hardware-required VideoToolbox encoding and 14 cumulative queue drops (this counter combines multiple pipeline stages). The iPhone CPU/memory fields were null as intended. Screenshots confirmed both remote pictures were present; Android's local preview was sideways and remains an orientation issue to address. This short run does not establish sustained heat reduction, smooth presentation, rear-camera/rotation correctness or equivalence to the production calling transport. The paired run was left active for the user, with its existing fifteen-minute limit.

## Interpretation

`captureFps` counts native camera callbacks; `encodedFps` counts compressed pictures; `receivedFps` counts complete peer pictures. Android output counts MediaCodec frame-rendered callbacks. iOS output counts AVSampleBufferVideoRenderer submissions, **not decoder completion or physical display presentation**. Do not compare those counters as equivalent hardware measurements. Use Instruments/Perfetto for presentation cadence and decoder attribution.

Hardware encoding is required by native configuration; Android reports the selected hardware codec name. iOS AVSampleBufferDisplayLayer decoding is platform managed and reported as unknown hardware status. CPU percentage is process CPU time per elapsed second (100% = one core); it excludes separately hosted WebView content processes. Battery temperature on Android is not SoC temperature. iOS exposes a thermal category, not degrees Celsius. A short run or a cool starting phone cannot establish sustained efficiency.

Summarize native exports and existing live-diagnostics JSONL with:

```sh
python3 scripts/yap/native-benchmark-summary.py iphone-native.jsonl android-native.jsonl web-live.ndjson --warmup-seconds 120
```

The script keeps devices/runs separate and removes duplicate web samples. It reports medians, low-tail FPS and high-tail gaps, not a fabricated overall score. Confirm matching settings and routes before interpreting differences. The existing screenshots/reports are useful historical context, not a controlled baseline for a new run.

## Validation record

Shared tests cover paired TLS transfer, incorrect pin/secret rejection, bounded framing, truncated input, H.264 framing, configuration validation and run cleanup. All ten tests pass. Android Release APK was installed on the Galaxy S25 Ultra over wireless ADB. The iOS Release app was signed with the newly added Personal Team and installed/launched on the connected iPhone 16 Pro Max. The web controls were deployed in Yap 1.3.95 through PR #616; health checks passed, and the actual Android app displays the benchmark page with its native controls enabled. Android system-bar insets were corrected after device screenshot verification. Physical two-phone capture, front/rear/orientation and thermal measurements remain pending. No temperature or FPS improvement has been established yet.
