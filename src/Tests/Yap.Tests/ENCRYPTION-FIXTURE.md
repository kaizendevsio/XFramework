# Encrypted browser fixture

This is disposable test infrastructure. It never registers fake users or calling gates in the production application.

Build the Yap.Tests project, then start its host with `YAP_FIXTURE_ENCRYPTION=1` and optionally `YAP_FIXTURE_PORT=5211`:

```powershell
$env:YAP_FIXTURE_ENCRYPTION = '1'
$env:YAP_FIXTURE_PORT = '5211'
dotnet run --no-build --project src/Tests/Yap.Tests -- --serve
```

HTTPS uses the configured ASP.NET development certificate. Browser origins must trust that certificate; do not disable certificate validation. The host permits `localhost`, `127.0.0.1` and `*.dev.localhost`. Use three isolated browser profiles, or aliases such as `alice.dev.localhost`, `bob.dev.localhost` and `charlie.dev.localhost` covered by the development certificate.

Log in as `fixture`, `callee` and `third`, each with password `fixture` (or the explicit `YAP_FIXTURE_PASSWORD` override). All three accounts belong to one initially empty group. IDs are generated once per process. Keep the host running throughout device recovery tests, because restarting deliberately discards its directories, encrypted archives and uploaded objects.

The browser uses the real login cookies, account/antiforgery checks, encryption setup, OpenPGP envelopes, upload/outbox flow, SSE, WSS and SFrame implementation. The backing services are local mocks: public key directories and recovery archives use actor-bound compare-and-swap; messages retain their signed envelope and sender device/revision; attachment bytes remain opaque in memory. Storage file IDs intentionally differ from message attachment link IDs. No mock supplies decryption keys to another account.

Run the fixture contract check with:

```powershell
dotnet test src/Tests/Yap.Tests --filter FullyQualifiedName~EncryptionFixtureEndpointTests
```

That test verifies three-account isolation, stale-update rejection, recipient refresh events, message field preservation and an 80 MiB opaque attachment hash through the BFF. Its dummy envelopes are not evidence of cryptographic correctness. The real browser smoke must additionally verify encrypted text on all three devices, encrypted attachment decryption, a fresh device recovering with the recovery key, and three-party audio with fresh keys after a member leaves. Production service validation and PostgreSQL persistence have separate tests.

The fixture enables the internal encrypted group-call gate solely for this process. It does not change production configuration or deployment readiness.
