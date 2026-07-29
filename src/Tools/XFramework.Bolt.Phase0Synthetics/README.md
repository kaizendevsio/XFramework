# Bolt Phase 0 Synthetics

This .NET 10 console tool exercises the externally exposed Bolt endpoint and emits one versioned JSON report. It exits non-zero if a required authenticated RPC, pub/sub, durable subscription, authorization, or transport-token-expiry check fails.

The target must use `wss://`. In deployed environments, Tailscale Serve terminates TLS and proxies to the Bolt Hub backend bound on host loopback. The tool uses platform certificate validation and does not install or accept a Bolt-specific CA. Its short-lived disposable tokens use the browser-compatible WebSocket query-string path so the deployment can verify that `tailscaled` does not retain those values or their JWT identifiers.

## Configuration

Required non-secret inputs can be supplied through environment variables or matching CLI options:

| Environment variable | CLI option |
|---|---|
| `BOLT_SYNTHETIC_TARGET` | `--target` |
| `BOLT_SYNTHETIC_TENANT_ID` | `--tenant-id` |
| `BOLT_SYNTHETIC_CREDENTIAL_ID` | `--credential-id` |
| `BOLT_SYNTHETIC_DEVICE_ID` | `--device-id` |

The synthetic consumes six role-specific credentials:

- `BOLT_SYNTHETIC_COMMUNICATIONS_TRANSPORT_TOKEN_FILE`: an RS256 `typ=bolt+jwt` token for `XFramework.Communications`.
- `BOLT_SYNTHETIC_COMMUNICATIONS_IDENTITY_SERVICE_TOKEN_FILE`: an IdentityServer-issued service token for caller `XFramework.Communications` and audience `XFramework.IdentityServer`. Communications-side ordering probes send it as `ServiceAccessToken`.
- `BOLT_SYNTHETIC_PORTAL_TRANSPORT_TOKEN_FILE`: an RS256 `typ=bolt+jwt` token for `XFramework.Portal`. Every user-side Bolt connection registers with this service identity.
- `BOLT_SYNTHETIC_PORTAL_IDENTITY_SERVICE_TOKEN_FILE`: an IdentityServer-issued service token for caller `XFramework.Portal` and audience `XFramework.IdentityServer`. The health RPC sends it as `ServiceAccessToken`, independently of the user actor token.
- `BOLT_SYNTHETIC_USER_ACTOR_TOKEN_FILE`: the ordinary HS512 user application JWT. It is never used for WebSocket transport authentication; it is supplied as `ActorAccessToken` on user-authorized RPC metadata and pub/sub subscribe, detach, acknowledgement, and permanent-unregister frames.
- `BOLT_SYNTHETIC_EXPIRY_TRANSPORT_TOKEN_FILE`: a second short-lived Communications transport token used only for the optional disconnect-on-expiry check.

Token files must be absolute, regular, non-linked files no larger than 16 KiB. On Unix they must be owner-readable with no group, other, or execute permissions. On Windows, readable access is limited to the current identity, Local System, and Administrators. Token values and file paths are never written to the report.

For local compatibility only, the tool can read `BOLT_SYNTHETIC_COMMUNICATIONS_TRANSPORT_TOKEN`, `BOLT_SYNTHETIC_COMMUNICATIONS_IDENTITY_SERVICE_TOKEN`, `BOLT_SYNTHETIC_PORTAL_TRANSPORT_TOKEN`, `BOLT_SYNTHETIC_PORTAL_IDENTITY_SERVICE_TOKEN`, `BOLT_SYNTHETIC_USER_ACTOR_TOKEN`, and `BOLT_SYNTHETIC_EXPIRY_TRANSPORT_TOKEN`. Transport, actor, and expiry `--*-token-env` options can select alternative environment variable names; no option accepts a token value directly.

Set `BOLT_SYNTHETIC_EXPIRY_TRANSPORT_TOKEN_FILE` to enable the optional expiration-disconnect check. The token must identify `XFramework.Communications` and expire within the bounded wait configured by `BOLT_SYNTHETIC_EXPIRY_MAX_WAIT_SECONDS`; grace is configured by `BOLT_SYNTHETIC_EXPIRY_GRACE_SECONDS`.

Set `BOLT_SYNTHETIC_REJECTED_COMMUNICATIONS_TRANSPORT_TOKEN_FILE` or `BOLT_SYNTHETIC_REJECTED_PORTAL_TRANSPORT_TOKEN_FILE` to verify that retired transport JWTs receive an explicit registration rejection. A timeout or unrelated transport failure does not pass this check.

The deployment workflow refreshes all six credentials directly from IdentityServer over its Tailscale HTTPS endpoint immediately before invoking this tool. The refresh helper uses platform trust only. It validates transport tokens as RS256, `typ=bolt+jwt`, issuer `XFramework.IdentityServer`, audience `XFramework.Bolt.Hub`, scope `bolt.service`, and the requested service plus credential generation. It separately validates each destination service token's caller and `XFramework.IdentityServer` audience, and validates the actor JWT as the configured HS512 application token. Bolt does not manage certificates, private CAs, root watchdogs, deployment leases, or certificate-rotation evidence.

```powershell
dotnet run --project src/Tools/XFramework.Bolt.Phase0Synthetics -- `
  --target wss://xeon-dev.example.ts.net:7000/bolt/ws `
  --tenant-id 00000000-0000-0000-0000-000000000001 `
  --credential-id 00000000-0000-0000-0000-000000000002 `
  --device-id phase0
```
