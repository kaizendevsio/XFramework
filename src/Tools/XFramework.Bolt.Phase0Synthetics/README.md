# Bolt Phase 0 Synthetics

This .NET 10 console tool emits one versioned JSON core report and exits non-zero if any required check fails. It accepts only `wss://` targets, uses platform certificate validation, sends bearer tokens in authorization headers, and never writes token values. `SyntheticReportWriter` validates the report schema, timestamps, statuses, safe result vocabulary, unique operation names, and required passing operations before serialization.

Required non-secret inputs can be supplied through environment variables or their matching CLI options:

| Environment variable | CLI option |
|---|---|
| `BOLT_SYNTHETIC_TARGET` | `--target` |
| `BOLT_SYNTHETIC_TENANT_ID` | `--tenant-id` |
| `BOLT_SYNTHETIC_CREDENTIAL_ID` | `--credential-id` |
| `BOLT_SYNTHETIC_DEVICE_ID` | `--device-id` |

Token files are the preferred credential source. Set `BOLT_SYNTHETIC_COMMUNICATIONS_TOKEN_FILE` and `BOLT_SYNTHETIC_USER_TOKEN_FILE` to absolute paths mounted read-only for the synthetic process. Files must be regular, non-linked files no larger than 16 KiB. On Unix they must be owner-readable and have no group, other, or execute permissions. On Windows, readable access is limited to the current identity, Local System, and Administrators. Each file is opened without sharing and read once; token values and file paths are never written to the report.

For local compatibility, when no token file is configured the tool reads `BOLT_SYNTHETIC_COMMUNICATIONS_TOKEN` and `BOLT_SYNTHETIC_USER_TOKEN`. The CLI options `--communications-token-env` and `--user-token-env` accept alternative environment variable names, never token values. The corresponding configuration environment variables are `BOLT_SYNTHETIC_COMMUNICATIONS_TOKEN_ENV` and `BOLT_SYNTHETIC_USER_TOKEN_ENV`.

Set `BOLT_SYNTHETIC_EXPIRY_TOKEN_FILE` to enable the optional expiration-disconnect check from a file. The environment-token compatibility sources are `BOLT_SYNTHETIC_EXPIRY_TOKEN`, `BOLT_SYNTHETIC_EXPIRY_TOKEN_ENV`, and `--expiry-token-env`. The token must have an `exp` inside the bounded wait configured by `BOLT_SYNTHETIC_EXPIRY_MAX_WAIT_SECONDS` / `--expiry-max-wait-seconds`. Grace is configured by `BOLT_SYNTHETIC_EXPIRY_GRACE_SECONDS` / `--expiry-grace-seconds`.

Set `BOLT_SYNTHETIC_REJECTED_COMMUNICATIONS_TOKEN_FILE` and/or `BOLT_SYNTHETIC_REJECTED_USER_TOKEN_FILE` to add retired-generation JWT rejection checks. These inputs are file-only and must differ from the matching current token. A configured retired token must receive an explicit Bolt registration rejection; a timeout or unrelated transport failure does not pass the check.

The durable check registers and detaches a unique subscriber, publishes three events while it is offline, reconnects and verifies ordered replay, cumulatively acknowledges the final sequence, submits a stale out-of-order acknowledgement and a duplicate acknowledgement, reconnects to verify bounded non-redelivery, then permanently unregisters and proves a later durable publish was not queued. It also rejects an authenticated user attempting to claim a reserved XFramework service identity. A live plaintext check is not sent by the .NET process because the authorized endpoint would require disclosing a valid bearer token over plaintext before the Bolt secure-transport handler can be reached.

## Deployment evidence wrapper

`scripts/run-bolt-phase0-synthetics.sh` is the production evidence boundary. It serializes runs with `flock`, removes stale reports before execution, uses the shared `verify-bolt-phase0-env.py` parser for protected settings, and quarantines all process output on `/dev/shm`. It requires a private, deployment-owned `BOLT_SYNTHETIC_TOKEN_REFRESH_COMMAND_PATH`. The hook receives no arguments or token values, runs with a minimal environment, and must atomically replace the current user and Communications tokens after obtaining them through IdentityServer HTTPS. It must emit no stdout/stderr and write a mode-private receipt to `BOLT_SYNTHETIC_REFRESH_RECEIPT` with this exact non-secret schema:

```json
{
  "schemaVersion": "bolt-phase0-token-refresh/v1",
  "status": "passed",
  "issuerUri": "xframework",
  "principalReference": "bolt-phase0-synthetic",
  "refreshedAtUtc": "2026-07-13T00:00:00Z",
  "tokenExpirationsUtc": {
    "communications": "2026-07-13T00:10:00Z",
    "user": "2026-07-13T00:10:00Z",
    "expiry": "2026-07-13T00:02:00Z"
  }
}
```

The hook receives `BOLT_SYNTHETIC_EXPIRY_ENABLED=true` only for the initial `canary` and `finalized` stages. In those stages it must also issue a one-use expiry token with no more than 570 seconds remaining and include the `expiry` receipt entry shown above. The wrapper gives the expiry probe a 600-second maximum, validates the disconnect, and destroys that file on every success or failure path. At every other stage the hook must create an empty, private expiry-token placeholder, omit `expiry` from `tokenExpirationsUtc`, and the wrapper clears `BOLT_SYNTHETIC_EXPIRY_TOKEN_FILE` for the container so the expiry wait does not run.

The wrapper verifies owner-only files, no symlinks, an exact bounded `iss` identifier, receipt-to-JWT expiration equality, a configurable 60-3600 second minimum lifetime (`BOLT_SYNTHETIC_MIN_TOKEN_LIFETIME_SECONDS`, default 60), bounded one-use expiry-token lifetime when enabled, and unchanged file identities throughout the run. The refresh hook separately requires a direct CA-validated HTTPS IdentityServer acquisition URL. Refresh output and synthetic stderr are never forwarded. The final artifact hashes `principalReference` to a short SHA-256 prefix rather than retaining the supplied value. The refresh receipt example is therefore the `canary`/`finalized` form; other stages contain only the `communications` and `user` expiration entries.

The following private, deployment-owned hooks are mandatory and receive only `XFRAMEWORK_ENV_FILE`, `BOLT_SYNTHETIC_TOKEN_MANIFEST`, `BOLT_SYNTHETIC_STAGE`, `BOLT_SYNTHETIC_PROBE_KIND`, and `BOLT_SYNTHETIC_PROBE_RECEIPT` in a minimal environment. They must read token values and raw `jti` markers from the private manifest's referenced files, return zero only when their check passes, and emit no stdout/stderr:

| Protected setting | Required evidence |
|---|---|
| `BOLT_SYNTHETIC_PROXY_MARKER_SCAN_COMMAND_PATH` | Exact current/retired token values absent from retained proxy/ingress data |
| `BOLT_SYNTHETIC_SEQ_MARKER_SCAN_COMMAND_PATH` | Exact values absent from Seq storage through an authoritative query |
| `BOLT_SYNTHETIC_TRACE_MARKER_SCAN_COMMAND_PATH` | Exact values absent from the configured trace backend |
| `BOLT_SYNTHETIC_PLAINTEXT_REJECTION_COMMAND_PATH` | Peer network plaintext `/bolt/ws` rejection without sending a bearer token |
| `BOLT_SYNTHETIC_REDIS_INTERRUPTION_COMMAND_PATH` | Controlled Redis interruption and recovery (`canary` stage) |
| `BOLT_SYNTHETIC_OLD_GENERATION_REJECTION_COMMAND_PATH` | Retired JWT and client-secret rejection with current HTTP/Bolt health intact (`finalized` stage) |

Every hook must write an owner-only `bolt-phase0-probe-receipt/v1` JSON document to `BOLT_SYNTHETIC_PROBE_RECEIPT`. The wrapper validates its probe identity, execution-window timestamps, and exact assertions before accepting it. Marker receipts require `retainedStoreQueried=true`, `matches=0`, and complete `tokensSearched`/`markersSearched` counts. Plaintext requires `plaintextRejected=true` and `bearerSent=false`. Redis requires an induced interruption, recovery, a passing post-recovery synthetic, and no observed data loss. Finalized old-generation evidence requires rejected old user/service JWTs and client secret plus green current HTTP and Bolt health. The safe receipts are embedded in the final evidence envelope; query text and backend credentials are prohibited.

The wrapper requires a unique, high-entropy JWT `jti` on every current or retired token in the private manifest. It independently streams every active Compose container's retained logs since token refresh and rejects an exact raw-token or `jti` match; the proxy, Seq, and trace hooks must query both forms. Its final `bolt-phase0-synthetic-evidence/v1` envelope embeds the validated core report, binds its exact bytes with `coreReportSha256`, and records only SHA-256 prefixes for marker correlation. It never records token values, raw markers, token paths, command paths, hook output, exceptions, or stack traces.

```powershell
dotnet run --project src/Tools/XFramework.Bolt.Phase0Synthetics -- `
  --target wss://bolt.example.test/bolt/ws `
  --tenant-id 00000000-0000-0000-0000-000000000001 `
  --credential-id 00000000-0000-0000-0000-000000000002 `
  --device-id phase0
```
