---
title: "Yap call efficiency and white-label implementation"
date: 2026-10-08
category: performance-issues
module: Yap
problem_type: performance_improvement
component: call_media
tags: [yap, bolt, benchmarks, hevc, white-label]
---

# Yap priorities 1-7

This work starts from develop `8ae24751` on `codex/yap-call-efficiency-white-label`.
PR #549 retains only startup improvements; this branch contains none of that PR's UI redesigns.
No deployment is part of this request. Version 1.3.82 identifies this release separately from
startup PR #549's 1.3.81.

| Priority | Implementation | Verification and practical limit |
| --- | --- | --- |
| 1. Benchmark current recovery and larger pictures | Production recovery parity, fixed 250ms freeze threshold including window edges, matched audio cohorts; baseline/current/native matrix at 360p30/720p30/1080p30; separate full WASM/relay/pacer runs | [Methodology and raw smoke evidence](yap-call-media-benchmark-current-recovery-and-resolution-profiles.md). Real-codec model omits production pacer/redundancy; full production experiment uses synthetic codecs. |
| 2. Protect constrained-network audio | Live sampled audio cannot erase the framing reserve; accepted Opus packet duration sets packet overhead; redundancy adds the repeated encrypted frame but shares the outer framing | Controller and loop tests exercise 20ms/60ms audio. Severe-link audio improvement must be measured rather than inferred. |
| 3. Reduce video recovery stalls | A complete independently decodable keyframe immediately supersedes obsolete incomplete pictures and cancels their NACKs. Uplink repairs use the audio-first pacer and a bounded RTT-aware lifetime | Loss/reorder/late-repair tests, including first useful uplink feedback at 500 / 1000ms RTT. No change to encryption or frame authentication. |
| 4. Reduce overhead where evidence supports it | Existing adaptive 60ms Opus packets retain their lower overhead reserve; retain compact feedback and the previously shipped SCTP correction | At the configured 130B overhead allowance, 20ms to 60ms reduces framing reserve from 52 to 17kbps. This is an allocation calculation, not an observed end-to-end bandwidth result. No speculative packet format rewrite. |
| 5. Expose recovery/congestion diagnostics | Each incoming stream reports recovery window, NACK/recovered/declined/abandoned counters, released/incomplete/skipped pictures and local drops. Outgoing diagnostics show total/video/Opus budgets, queue delay and congestion state | Snapshot merge is tested by stream ID; missing measurements remain absent. Recovery counters are cumulative, codec timings remain local. |
| 6. Hardware-efficient HEVC | Negotiate HEVC only with a positive power-efficient encode probe and all peers advertising hardware-efficient HEVC decode. Preserve unknown/older peer H.264 compatibility. Use Annex B, read actual SPS profile/level, retain authenticated relay codec allowlist | Browser/unit tests plus real Chromium encode/decode roundtrips at 1080p30, 1440p30 and 2160p30, 30/30 pictures. Power-efficient is a browser capability report, not proof of GPU use or lower phone temperature. |
| 7. White-label tenant origins | Exact configured host allowlist selects tenant/role; reject unknown hosts and cross-tenant sessions before upstream work. Public names/assets/accent/manifest and offline branding per origin | [Configuration, isolation and scaling constraints](../architecture-patterns/yap-white-label-host-routing.md). Same backend/database; tenant provisioning and ingress DNS/TLS remain operator tasks. |

HEVC follows the [W3C codec registration](https://www.w3.org/TR/webcodecs-hevc-codec-registration/):
without a decoder description, input must be Annex B, including parameter sets on key access units.
The encoder is explicitly configured with `hevc.format=annexb`; a browser that ignores that dictionary
is not advertised as an HEVC encoder. Some encoders accepted 60fps probes but failed encoder creation on this desktop. HEVC therefore verifies one synthetic picture before stream publication and before later size/FPS tier changes. A refused 60fps change keeps the existing encoder intact and retries the same resolution at 30fps, limiting further attempts to 30fps for the call. That picture is never sent or counted in diagnostics. The profile string is recovered from the authenticated SPS
rather than assuming the browser emits the requested profile/level. Codec negotiation tests cover
legacy peers, software HEVC refusal and wire-codec mapping.

Actual phone capture, battery/thermal behavior and sustainable 60fps remain device tests. A desktop
roundtrip proves codec interoperability at that configuration, not sustained real-camera FPS.
Benchmark runs and final validation results are appended after completion.

On a 512kbps link, the default 20ms Opus allocation reserves 84kbps without redundancy and 139kbps with it (one repeated encrypted media frame, shared outer framing, bundle length fields). That leaves 373kbps for video with redundancy. The actual negotiated-bundle test validates its wire length and allocation. These figures describe budgeting; production netem measurements determine the observed result.

## Validation

The current implementation passed 316 Yap server/UI tests, 319 Yap client tests,
114 focused transport/congestion/recovery regressions and 24 browser-service/authenticated-media
tests. JavaScript validation passed 101 video tests, nine branding/accent tests and three metric tests.
Six recovery parity cases also passed against both the archived baseline and current production source.
The hosted benchmark repeats are recorded in the linked methodology document after completion.

A real Chromium live-upgrade smoke check initialized HEVC at 2560x1440/30fps,
attempted 60fps (native encode refused), and then encoded ten further pictures with
the untouched 30fps encoder: 10/10 outputs, encoder still configured. This verifies
failure preservation, not a sustained-camera performance claim.
