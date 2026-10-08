# Call media benchmark

`.github/workflows/call-media-benchmark.yml` reports two deliberately separate experiments.

The real-codec comparison runs native WebRTC and Bolt's modeled datagram path with the same moving canvas,
continuous synthetic audio, picture size, FPS, target bitrate and receiver TURN impairment. Profiles are
640×360, 1280×720 and 1920×1080 at 30 FPS. The clean video targets are 1500, 2500 and 4000 kbps respectively;
the two 512 kbps profiles target 300 kbps video at every picture size. These are benchmark inputs, not product
resolution limits. Native WebRTC is told to maintain resolution; Bolt's model retains it too.

Native uses its own congestion control and a 20-byte SFrame-sized encoded transform. Bolt uses the checked-out
production encoder settings, actual SFrame WASM, production RTC channel and sidecar, and actual .NET
`SendRateController`/`AudioPacketization`. Its in-page recovery model follows `VideoRecoveryBuffer`; the workflow
first compares every operation in deterministic traces against the production C# source, including a slowly
arriving large keyframe, missing tails, reorder, temporal dependencies, independent keyframes, orientation and
buffer bounds. Baseline recovery policy is detected using that snapshot's actual buffer. A parity failure stops
the measurement rather than silently benchmarking stale recovery rules.

The model omits the production pacer, relay media lanes/congestion reports, audio redundancy and relay repair cache.
It answers NACKs at the sender. Consequently this table cannot establish whether production pacing or redundancy
changes fixed a call regression.

The second experiment runs the existing `BrowserCallTests` through the actual AOT Blazor WASM media service,
pacer, SFrame, BoltServer relay, relay recovery cache, sidecar and coturn. Both participants prefer 1080p30; the
production controller and ladder can adapt to the network. Encoded payloads and the decoder are synthetic.
The clean and 512 kbps / 1000 ms RTT / 3% loss profiles shape participant A's receiver leg identically for
baseline/current. The test's existing quality assertions and failures stay visible in JSON. A quality failure
with a completed measurement is reported; a setup failure fails the job. These results test transport behavior,
not real codec quality, decoder CPU, phone temperature or battery life. There is no native comparator for this table.

## Comparable metrics

- `metricVersion: 2` uses one fixed 250 ms video gap threshold, independent of the achieved FPS. It counts startup,
  interior and trailing gaps in the measured window. `frozenSeconds` is only each gap's excess above 250 ms;
  `longestGapMs` and counts are also retained. Version 1's threshold depended on each path's mean FPS and omitted
  the window edges, so old freeze numbers cannot be compared directly.
- Video delay is the canvas draw timestamp decoded from the picture's barcode to the decoded frame callback.
  Samples and barcode success percentage are reported so low coverage cannot masquerade as low latency.
- Audio transport delay uses the same page clock on both paths, from sender encoded output to receiver encoded
  arrival. Native matches RTP encoded-frame timestamps; Bolt matches media sequence numbers after decryption.
  It excludes capture/encoding and audible playout. A missing encoded transform or no matched samples fails the run.
- Audio delivery counts the same cohort: packets encoded during the measurement window, with unique matched
  arrivals accepted during the window and a fixed 5-second drain. Warmup and post-window sends cannot inflate it.
  Wire and video measurement windows stop before the drain. Unmatched packets at drain completion count as undelivered.
- Sender and receiver wire metrics are iptables byte/packet deltas, including IP/UDP headers and TURN, DTLS, SCTP,
  feedback and repairs. Reverse-leg bytes include SCTP acknowledgements as well as media feedback and NACKs.
- The separate production experiment retains the existing longest-frame-gap metric with window edges. Its audio
  delivery is over the observed sequence span and its audio delay samples are cumulative over the call. It cannot
  account for completely unseen leading/trailing audio packets and must not be merged with the modeled cohort metrics.

## Snapshots and repeats

The default baseline is `8ae24751` (the develop snapshot before this task). The current branch supplies the
measurement harness to both checkouts; production assets and assemblies remain each snapshot's own. Both
sidecars are built from their corresponding sources. JSON includes the full implementation revision, browser
version, input profile, metric version and direct-smoke flag. Production artifacts include a revision file.

Dispatch after pushing the branch:

```powershell
gh workflow run call-media-benchmark.yml --ref codex/yap-call-efficiency-white-label -f baseline_ref=8ae24751 -f seconds=60 -f repeats=3 -f production=true
```

Each run remains a separate table row; modeled runs alternate path order on even repeats. Netem loss remains
random and the hosted runner's scheduling and Chrome software codec can vary. Use repeated results to assess
variation; a single run is descriptive, not evidence of a statistically reliable improvement. Production snapshots
run in separate jobs, so they also have runner variance. Do not infer phone thermal behavior from CI throughput.

Focused verification:

```powershell
dotnet test src/Tests/Bolt.Rtc.IntegrationTests/Bolt.Rtc.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~RecoveryBufferBenchmarkTests
node --test src/Tests/Bolt.Rtc.IntegrationTests/bench/metrics.test.mjs
go run github.com/rhysd/actionlint/cmd/actionlint@v1.7.12 .github/workflows/call-media-benchmark.yml
```

For a local smoke run, set `BENCH_DIRECT=1`, `BENCH_MODE=native` or `bolt-current`, `BOLT_RTC_TURN_SECRET`,
`BENCH_WIDTH`, `BENCH_HEIGHT`, `BENCH_FPS`, `BENCH_VIDEO_KBPS`, `BENCH_SECONDS` and `BENCH_OUT`; Bolt also
requires the built `BOLT_RTC_SIDECAR`. This verifies codecs/metrics over direct loopback. It measures neither
TURN efficiency nor network loss recovery, and its output is marked `directSmokeOnly: true`.
