# Running the native video benchmark

## Scope

This is a separate, video-only two-phone prototype, not a replacement for Yap calling. Both hosts show the existing HTTPS web application. Open `/diagnostics/native-media` in that application after deploying the client page from this branch. Native views display video above the web controls. No video buffers cross the WebView bridge.

The prototype uses H.264, native hardware-required encoding, and an encrypted, explicitly paired LAN TCP connection. It deliberately excludes normal Yap call admission, audio, Bolt SFrame, peer ICE, TURN, congestion adaptation and group calls. Do **not** compare transport performance directly with the existing Bolt UDP path. This first experiment answers whether the native video path materially improves sustained capture/display and heat. It does not justify a production migration by itself.

## Android

Run `scripts/yap/build-native-android.ps1`. The signed Release APK is under `src/Presentation/XFramework.Yap.Native.Android/bin/Release/net10.0-android/android-arm64/publish/`. This is development signing, not Play Store distribution. Install the `-Signed.apk` with `adb install -r <path>` or transfer it to your phone and allow installation from that source. The application label is **Yap native benchmark**, separate from the existing PWA.

## iPhone

Use .NET 10's iOS workload compatible with installed Xcode. On the supplied Mac, SDK 10.0.102 / iOS 26.2.10191 matches Xcode 26.2. `bash scripts/yap/build-native-ios.sh --unsigned` validates a device build but does not produce an installable signed app.

Do not use existing employer/organization profiles. In Xcode Settings → Accounts, sign in with your personal Apple Account. Create a temporary iOS App project with bundle identifier `io.kaizendevs.yap.nativebenchmark`, choose your **Personal Team**, enable automatic signing, select the paired iPhone and build once. This creates a development identity/profile. Apple may require a unique bundle ID; if it does, set the same `ApplicationId` on the .NET build. A free Personal Team profile expires after seven days and must be renewed/reinstalled. No App Store review is required for this development installation.

Then set `YAP_CODESIGN_KEY` to that Apple Development identity and `YAP_CODESIGN_PROVISION` to the new profile's UUID/name; run `bash scripts/yap/build-native-ios.sh`. Keep passwords and signing keys out of the repository. Install the signed `.app` with Xcode Devices or `xcrun devicectl device install app --device <device-id> <app-path>`. Enable Developer Mode/trust if iOS requests it.

Apple: [personal device testing](https://developer.apple.com/help/account/basics/about-your-developer-account), [device signing requirements](https://help.apple.com/xcode/mac/current/en.lproj/dev5a825a1ca.html).

## Pair and run

1. Put both phones on the same non-isolated Wi-Fi. Allow camera/local-network permission. Open the prototype and enter the HTTPS Yap origin in its top address field if the default address is not reachable. The page must exist on that origin; the APK does not update the server.
2. End regular Yap calls. Select 1080p30 and 6 Mbps on **both** phones. Use the same camera as the web comparison. The setting is per sender, not copied by the invitation.
3. On the host, enter its Wi-Fi IPv4 address from phone Wi-Fi settings; press **Create paired test**. Copy the temporary invitation to the second phone and press **Join paired test**. The invite pins the TLS certificate and authenticates the peer. Do not put it in benchmark reports or public chats.
4. Both native cameras start when pairing completes. Stop cancels the run on both ends. Navigating, backgrounding or disconnecting stops capture. Pairing waits at most three minutes; a run is capped at fifteen minutes. Re-pair after stopping or changing camera/FPS.
5. Test front/rear cameras separately, then rotate portrait → landscape → portrait. Record whether either image is cropped, mirrored unexpectedly or sideways. These orientation cases require physical verification on both platforms.
6. Cool devices before measurements. Run 12 minutes at 1080p30 (discard first two), followed by an equivalent web call with both microphones muted, same brightness/scene/bitrate and network where possible. Repeat in reverse order. Only then test 1080p60. If a camera/codec rejects 60 FPS, record the rejection rather than substituting 30 FPS.
7. Stop and press **Export numeric report**. iOS uses its share sheet; Android copies numeric JSONL to the clipboard. Reports are also stored in the app's private `benchmarks/` directory. Never enable camera/microphone data logging.

## Interpretation

`captureFps` counts native camera callbacks; `encodedFps` counts compressed pictures; `receivedFps` counts complete peer pictures. Android output counts MediaCodec frame-rendered callbacks. iOS output counts AVSampleBufferVideoRenderer submissions, **not decoder completion or physical display presentation**. Do not compare those counters as equivalent hardware measurements. Use Instruments/Perfetto for presentation cadence and decoder attribution.

Hardware encoding is required by native configuration; Android reports the selected hardware codec name. iOS AVSampleBufferDisplayLayer decoding is platform managed and reported as unknown hardware status. CPU percentage is process CPU time per elapsed second (100% = one core); it excludes separately hosted WebView content processes. Battery temperature on Android is not SoC temperature. iOS exposes a thermal category, not degrees Celsius. A short run or a cool starting phone cannot establish sustained efficiency.

Summarize native exports and existing live-diagnostics JSONL with:

```sh
python3 scripts/yap/native-benchmark-summary.py iphone-native.jsonl android-native.jsonl web-live.ndjson --warmup-seconds 120
```

The script keeps devices/runs separate and removes duplicate web samples. It reports medians, low-tail FPS and high-tail gaps, not a fabricated overall score. Confirm matching settings and routes before interpreting differences. The existing screenshots/reports are useful historical context, not a controlled baseline for a new run.

## Validation record

Shared tests cover paired TLS transfer, incorrect pin/secret rejection, bounded framing, truncated input, H.264 framing, configuration validation and run cleanup. Android Release APK and unsigned iOS Release device build have been produced. Web client compilation passes. Physical front/rear/orientation/thermal tests and personal-team signed iPhone installation are still pending. No temperature or FPS improvement has been established yet.
