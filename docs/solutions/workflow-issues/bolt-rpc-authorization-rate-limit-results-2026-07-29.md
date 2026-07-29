# Bolt RPC Authorization and Rate-Limit Results

**Date:** 2026-07-29
**Plan:** [`2026-07-29-001-fix-bolt-rpc-authorization-and-rate-limits-plan.md`](../../plans/2026-07-29-001-fix-bolt-rpc-authorization-and-rate-limits-plan.md)

## Result

The approved security plan is implemented without a Bolt wire-version change or a Hub command-authorization matrix.

- Generated Bolt handlers validate an IdentityServer-issued destination service token before request validation or endpoint-service resolution.
- The validated token caller is bound to the Hub-verified frame sender. Optional required scopes and allowed callers remain destination-handler policies.
- Missing or invalid tokens return `401`; caller, scope, and sender-policy failures return `403`; unavailable signing-key validation infrastructure returns `503`.
- Successful base JWT validations use a bounded 1,024-entry cache that cannot outlive token expiry. Signing-key refresh is single-flight and stores only the active key set rather than attacker-controlled `kid` entries. Unknown key IDs trigger at most one globally throttled refresh, so newly rotated keys become available without an attacker creating per-key cache entries or HTTP fanout.
- Push requires an explicit recipient in .NET and Browser clients. Recipient hash zero is a route miss and cannot fan out.
- Bolt Hub applies allocation-free request and inbound-byte token buckets per authenticated `QuotaKey`, shared across pooled connections. Unary and large-RPC admission return `429`; over-limit Push is dropped and measured.
- XFramework Bolt Hub requires an `IBoltTopicAuthorizer`. The reusable Bolt server keeps authentication and topic authorization optional. Communications remains the current domain policy. Durable acknowledgement revalidates both topic policy and exact live subscription ownership immediately before mutating the durable store.

## Hub Limits

The Hub configuration enables:

| Limit | Value |
|---|---:|
| Logical RPC requests per second | 800,000 |
| Request burst | 200,000 |
| Inbound logical RPC/Push bytes per second | 1.25 GiB |
| Inbound byte burst | 256 MiB |

These values stay above the previous single-principal benchmark peak with substantial headroom while retaining bounded admission. Rejections are exposed through aggregate metrics and health diagnostics without principal labels.

## Verification

All commands ran on Windows 11, .NET 10, x64 RyuJIT/AVX2.

| Suite | Result |
|---|---:|
| Complete Bolt suite in Phase 0 CI, including PostgreSQL topic authorization | 551 passed |
| Local Bolt suite without Docker-backed fixtures | 540 passed, 7 skipped |
| Communications tests | 92 passed |
| Communications validator follow-up | 93 passed |
| IdentityServer unit tests | 31 passed |
| Source-generator tests | 14 passed |
| Browser tests | 19 passed |
| Storage integration fixture assessment | Reached Docker/PostgreSQL; fixture cannot start its synthetic service because it does not provide centralized service identity |
| Wallets integration fixture assessment | Same pre-existing synthetic-host limitation as Storage |
| Full solution build | 0 errors |

GitHub Actions ran the complete Bolt suite against PostgreSQL and Redis-backed fixtures: 551 tests passed with no failures or skips. A Docker 29.3.0 host was also reached through the `xeon-dev` Tailscale path while assessing Storage and Wallets.

Those two module fixtures construct synthetic Bolt Hub, service, and caller applications but do not host IdentityServer or replace the centralized transport-token and destination service-token providers. They therefore stop during fixture startup once mandatory service identity is enabled. This is test-infrastructure debt exposed by the centralized authentication rollout, not evidence of a production defect; it is not counted as a passing gate. Production compose declares IdentityServer authority, generation, and per-service credentials for Bolt Hub and every deployed Bolt client, and the coordinated staging deployment remains the real environment gate.

Security regressions cover wrong audience, malformed/expired/invalid tokens, token/sender substitution, unavailable and rotated signing keys, failed-token cache behavior, pooled-connection quota bypass, malformed and spoofed frame admission, zero-recipient Push, large-RPC accounting, limiter replenishment and cleanup, generated-handler unary/large-RPC parity, validation ordering, unknown topics, denied durable acknowledgements, and acknowledgement/subscription replacement races.

### Staging validator follow-up

The coordinated staging synthetic run subsequently exposed a signature-provider lifetime defect that unit tests using only one destination token had not exercised. `ServiceTokenValidator` imported each signing key into an owned RSA instance and disposed it after validation, while IdentityModel's default signature-provider cache could retain a provider backed by that disposed RSA instance. The first destination token validated, but a second distinct token signed by the same IdentityServer key could return `401`.

Imported verification keys now disable signature-provider caching, matching the IdentityServer token-issuance path and keeping provider lifetime within the owned RSA lifetime. A regression validates separate Portal and Communications tokens, with distinct callers and the same signing key, against the IdentityServer audience. The focused validator suite passes 11 tests, the complete Communications suite passes 93 tests, and the Phase 0 synthetic suite passes 42 tests with one platform-specific symbolic-link test skipped.

The staging gate also keeps the durable replay subscription active until its cumulative, stale, and duplicate acknowledgement frames pass the IdentityServer RPC processing barrier. Persistence is reported only after the next subscription observes no redelivery. Previously the runner disposed the subscription before sending those acknowledgement frames, so the Hub correctly rejected them as coming from a detached owner and the next connection replayed the unacknowledged message. This was a synthetic lifecycle defect; Hub ownership checks, durable-store behavior, and Bolt's at-least-once delivery contract are unchanged.

Permanent durable unregistration now also removes an authorized offline subscription after its live session has detached, matching the public client API contract. A permanent unregister remains rejected while another live connection owns the same subscriber identity, preventing a stale session from deleting active durable state.

The final ownership review also exposed a scheduling race where a successful reliable-send waiter could resume immediately before its pooled buffer decremented `PendingSends`. Success notification now follows buffer release, while timeout paths continue retaining ownership until a cancellation-ignoring physical write completes. The near-deadline lifecycle test passed 30 consecutive isolated repetitions; the synchronized outbound-stream cleanup test passed 10.

## Performance Gate

BenchmarkDotNet used three launches, five warmups, fifteen measured iterations, and a 250 ms minimum iteration time. The enabled and disabled runs use the same code; `BOLT_BENCH_RATE_LIMITS=0` is the only toggle.

| Concurrency | Limiter | Mean, run 1 / run 2 | p95, run 1 / run 2 | Mean of runs | Mean p95 of runs |
|---:|---|---:|---:|---:|---:|
| 1 | Disabled | 196.3 / 237.8 us | 232.6 / 339.4 us | 217.1 us | 286.0 us |
| 1 | Enabled | 201.5 / 204.0 us | 248.6 / 267.8 us | 202.8 us | 258.2 us |
| 64 | Disabled | 777.1 / 757.9 us | 1,288.6 / 1,114.9 us | 767.5 us | 1,201.8 us |
| 64 | Enabled | 777.4 / 777.0 us | 1,128.5 / 1,174.8 us | 777.2 us | 1,151.7 us |

Because individual Hyper-V runs are multimodal, the gate uses all raw measured `WorkloadResult` samples from both complete repetitions rather than averaging two reported percentiles:

| Concurrency | Limiter | Raw samples | Combined mean | Combined p95 |
|---:|---|---:|---:|---:|
| 1 | Disabled | 85 | 217.289 us | 322.480 us |
| 1 | Enabled | 87 | 202.729 us | 264.165 us |
| 64 | Disabled | 83 | 767.622 us | 1,292.100 us |
| 64 | Enabled | 84 | 777.191 us | 1,177.300 us |

Enabling quotas changes combined mean by `-6.7%` at concurrency 1 and `+1.2%` at concurrency 64. Combined p95 improves by `18.1%` and `8.9%`, respectively, and allocation remains effectively unchanged. This combined-sample analysis satisfies the 5% mean/p95 gate without selecting one favorable run. No authorization or limiter behavior was bypassed for the benchmark.

The warm generated-handler path was also measured separately with the real service-token validator, successful-validation cache, invocation authorizer, request serialization, generated DI scope, endpoint service, and response serialization. Across 20,000 calls after 1,000 warmups it recorded 7.646 us mean, 9.100 us p95, 130,782 operations/second, 4,632 allocated bytes per complete invocation, and 156.2 ms process CPU. This is local regression evidence for the authorization path, not a cross-transport comparison.

Durable benchmark artifacts included with this change:

- [Disabled run 1](./bolt-rpc-authorization-rate-limit-results-2026-07-29/disabled-run-1.md)
- [Disabled run 1 log with raw samples](./bolt-rpc-authorization-rate-limit-results-2026-07-29/disabled-run-1.log)
- [Enabled run 1](./bolt-rpc-authorization-rate-limit-results-2026-07-29/enabled-run-1.md)
- [Enabled run 1 log with raw samples](./bolt-rpc-authorization-rate-limit-results-2026-07-29/enabled-run-1.log)
- [Disabled run 2](./bolt-rpc-authorization-rate-limit-results-2026-07-29/disabled-run-2.md)
- [Disabled run 2 log with raw samples](./bolt-rpc-authorization-rate-limit-results-2026-07-29/disabled-run-2.log)
- [Enabled run 2](./bolt-rpc-authorization-rate-limit-results-2026-07-29/enabled-run-2.md)
- [Enabled run 2 log with raw samples](./bolt-rpc-authorization-rate-limit-results-2026-07-29/enabled-run-2.log)

The first limiter implementation regressed sequential mean by about 14.5%. Replacing framework token-bucket leases with a small allocation-free principal bucket brought the aggregate A/B results inside the 5% gate. BenchmarkDotNet did not expose a separate CPU-time counter on this Hyper-V host; CPU was therefore recorded in the dedicated warm generated-handler measurement.

## Deployment Readiness

The checked-in compose contract provisions centralized IdentityServer authority and credentials for Bolt Hub, Communications, Notifications, Storage, Attendance, SmsGateway, Wallets, Inventario, POS, Portal, and Operations Dashboard. The deployment workflow validates that contract, builds and rolls out all wire-v2 components together, then runs service health and Bolt registration smoke checks. Mixed Bolt wire versions intentionally fail registration, so partial deployment is not supported.

Storage and Wallets synthetic integration hosts should be modernized separately to issue real test transport and destination service tokens, or to install explicit test doubles for both trust boundaries. That work improves module-level test coverage but does not change the production authentication design implemented here.
