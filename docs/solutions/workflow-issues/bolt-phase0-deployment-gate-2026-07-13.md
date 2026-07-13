---
title: "Bolt Phase 0 Deployment Gate"
date: 2026-07-13
category: workflow-issues
module: Bolt
problem_type: workflow
component: deployment
severity: critical
applies_when:
  - "Preparing, reviewing, deploying, rolling back, or recovering Bolt Phase 0"
  - "Collecting security, provenance, synthetic, rotation, or live rollout evidence"
tags: [bolt, phase-0, deployment, recovery, tls, credentials, synthetics]
status: active
---

# Bolt Phase 0 Deployment Gate - 2026-07-13

## Status

Phase 0 containment and deployment automation merged through PR [#352](https://github.com/kaizendevsio/XFramework/pull/352) at `fb169a530d6e80d64aeab7c73029fabea08d3152`. Committed Ubuntu run [29229428707](https://github.com/kaizendevsio/XFramework/actions/runs/29229428707) passed the Release build, Bolt Hub container build, all .NET and privileged Python gates, all 40 `Bolt.Phase0Synthetics.Tests`, and the Linux wrapper contract scenarios. It did not run authenticated WSS synthetics against xeon-dev. The runner deployment key, distinct IdentityServer TLS material, dedicated synthetic principal, and protected inputs are now provisioned. The P0-R21 and P0-R22 rollout corrections are implemented locally and await final independent review, committed Ubuntu CI, merge, and live execution. Phase 0 remains `Contained`, not `Verified`: xeon-dev has not received the one-time sealed watchdog bootstrap, the current Hub is unchanged, and no complete staged run has produced live evidence. Do not disable `RequireSecureTransport`, restore Audit mode, or deploy a pre-containment image to bypass this gate. Production promotion remains blocked until Phase 1 removes the shared service-signing trust boundary.

## Implemented Automation

- `docker-compose.yml` exposes only Hub HTTPS `8443` through the host Bolt port. Loopback HTTP `8080` is retained only for in-container liveness/readiness probes.
- Every Compose Bolt client uses `wss://bolt-hub:8443/bolt/ws`, requires secure transport, and mounts the dedicated CA. Only the Hub mounts the full chain and private key.
- The shared image installs the mounted CA before starting .NET and never disables certificate validation.
- `scripts/verify-bolt-phase0-compose.py` semantically verifies the rendered manifest and refuses mutable image tags, plaintext URLs/listeners, missing client trust, key sharing, replica drift, Media enablement, scope-shape overrides, or weakened limits. It enforces approved repositories and every exact Phase 0 quota.
- `scripts/verify-bolt-phase0-tls.sh` verifies file permissions, chain, hostname, key match, current validity, and at least 24 hours of remaining certificate lifetime.
- `scripts/verify-bolt-phase0-runtime.py` records redacted runtime identity, exact registry digests, listener/process topology, resolved mounts, generation convergence, and health without archiving container environment variables.
- The full xeon-dev workflow freezes legacy service deployment and enforces Hub-only promotion, IdentityServer/Communications canary, authenticated synthetics, observation, bounded batches, credential finalization, rollback drill, qualification, and immediate fail-closed recovery.
- Recovery tools, manifests, pins, TLS evidence, launcher, and systemd fragments are digest-bound into a root-owned LKG bundle. A stable operator-installed launcher and root activation helper are never overwritten by the workflow; failure recovery bypasses lease freshness and restores immediately or stops Hub.
- Every synthetic stage starts a supervised 30-second heartbeat against the fixed root-owned lease manager before acquiring its evidence lock. A failed or unexpectedly terminated heartbeat makes the synthetic fail and signals its parent, keeping the 600-second first-rollout lease fresh through the longest bounded synthetic execution.
- The watchdog timer remains enabled and active throughout candidate quarantine, qualification, sealing, and pointer publication. The root helper uses the deployment lease lock only for the two candidate-path swaps, and the pinned failure handler invokes the fixed `ensure-watchdog` command before and after forced recovery.

## One-Time Root Bootstrap

The workflow cannot bootstrap or update its own root trust boundary, and the root bootstrap refuses an ordinary `github-runner` checkout. Before execution, an operator must copy only the reviewed bootstrap bundle into a root-only staging tree. The copy is intentionally performed before review and hashing; the operator reviews the staged bytes and records/compares their hashes afterward, when the deployment user can no longer change them.

```bash
stage=/root/xframework-bolt-phase0-bootstrap-20260713
sudo install -d -o root -g root -m 0700 \
  "$stage/deploy/systemd" "$stage/scripts"

sudo cp --no-dereference -- \
  deploy/bootstrap-xframework-bolt-phase0-root.sh \
  "$stage/deploy/bootstrap-xframework-bolt-phase0-root.sh"
sudo cp --no-dereference -- \
  deploy/systemd/xframework-bolt-phase0-watchdog.service \
  deploy/systemd/xframework-bolt-phase0-watchdog.timer \
  "$stage/deploy/systemd/"
sudo cp --no-dereference -- \
  scripts/manage-bolt-phase0-root.py \
  scripts/run-bolt-phase0-watchdog.sh \
  scripts/manage-bolt-phase0-deployment-lease.py \
  scripts/verify-bolt-phase0-qualification.py \
  "$stage/scripts/"

sudo chown -R root:root "$stage"
sudo find "$stage" -xdev -type d -exec chmod 0700 {} +
sudo find "$stage" -xdev -type f -exec chmod 0400 {} +
sudo chmod 0500 "$stage/deploy/bootstrap-xframework-bolt-phase0-root.sh"
test -z "$(sudo find "$stage" -xdev -type l -print -quit)"

# Review these staged files and compare these post-copy hashes with the approved release.
sudo sha256sum \
  "$stage/deploy/bootstrap-xframework-bolt-phase0-root.sh" \
  "$stage/deploy/systemd/xframework-bolt-phase0-watchdog.service" \
  "$stage/deploy/systemd/xframework-bolt-phase0-watchdog.timer" \
  "$stage/scripts/manage-bolt-phase0-root.py" \
  "$stage/scripts/run-bolt-phase0-watchdog.sh" \
  "$stage/scripts/manage-bolt-phase0-deployment-lease.py" \
  "$stage/scripts/verify-bolt-phase0-qualification.py" \
  | sudo tee "$stage.reviewed.sha256"

# Stop xframework-bolt-hub, then execute only the absolute staged bootstrap.
sudo "$stage/deploy/bootstrap-xframework-bolt-phase0-root.sh" "$stage"
```

The bootstrap requires itself to be the exact absolute file under the supplied staging root. It opens every staging path component descriptor-relatively and rejects a non-root owner, group/world-writable parent, symlink, non-regular component, hard link, or any writable component. Companion bytes are copied through `O_NOFOLLOW` descriptors, read completely to EOF under a 4 MiB ceiling despite short reads or `EINTR`, checked twice for exact size, stable identity, metadata, and content, then atomically installed without following a source pathname. The bootstrap refuses to proceed while `xframework-bolt-hub` is running. A nonzero container inspection is accepted as an absent container only when a separate bounded `docker container ls -a --no-trunc --filter name=^/xframework-bolt-hub$ --format '{{.Names}}'` returns successfully with exactly no names and a bounded `docker info --format '{{.ServerVersion}}'` returns a nonempty version. A present named container, daemon, socket, permission, empty-version, malformed-state, timeout, or other inspection failure aborts bootstrap. The root helper and no-LKG watchdog branch enforce the same proof. It installs these fixed root-owned files:

```text
/usr/local/sbin/xframework-bolt-phase0-root                         root:root 0555
/usr/local/sbin/xframework-bolt-phase0-watchdog                     root:root 0555
/usr/local/libexec/xframework-bolt-phase0/manage-bolt-phase0-deployment-lease.py root:root 0555
/usr/local/libexec/xframework-bolt-phase0/verify-bolt-phase0-qualification.py root:root 0444
/usr/local/libexec/xframework-bolt-phase0/deployment-lease.lock     root:github-runner 0440
/etc/systemd/system/xframework-bolt-phase0-watchdog.service         root:root 0644
/etc/systemd/system/xframework-bolt-phase0-watchdog.timer           root:root 0644
```

It also creates the exact state layout:

```text
/home/github-runner/xframework-deploy                              root:root 0755
/home/github-runner/xframework-deploy/runs                         root:root 0755
/home/github-runner/xframework-deploy/quarantine                   root:root 0700
/home/github-runner/xframework-deploy/phase0-last-known-good       root:root 0755
/home/github-runner/xframework-deploy/phase0-watchdog              github-runner:github-runner 0700
/home/github-runner/xframework-deploy/hooks                        github-runner:github-runner 0700
```

The fixed lease manager owns the external deployment lease and failure controller. The root helper owns candidate preparation and activation. Both open the same pre-created `root:github-runner 0440` deployment lock read-only with `O_NOFOLLOW`; its complete parent chain is root-owned and nonwritable. They verify descriptor, directory-entry, pathname, parent, and inode identity before and after `flock` and again before unlock. The deployment user cannot create, rename, unlink, or replace this lock, including while either critical section is active. Bootstrap reruns preserve its inode.

The generated `/etc/sudoers.d/xframework-bolt-phase0-root` permits only these fixed commands:

```text
/usr/local/sbin/xframework-bolt-phase0-root verify-bootstrap
/usr/local/sbin/xframework-bolt-phase0-root ensure-watchdog
/usr/local/sbin/xframework-bolt-phase0-root prepare-run *
/usr/local/sbin/xframework-bolt-phase0-root activate *
```

No interpreter, stdin program, shell, file utility, or `systemctl` command is allowed through sudo. The helper validates every argument and fixed path internally. With no LKG pointer, bootstrap is valid only while the Hub is verified stopped. During the first rollout, the stable watchdog permits a running Hub only while the fixed root-owned lease manager validates a fresh, schema-complete lease bound to the prepared run directory; an absent, stale, any-future-dated, malformed, wrongly owned, or incorrectly permissioned lease stops Hub. Its fail-closed path bounds stop at 40 seconds plus a 5-second kill grace, always verifies state, then bounds kill at 10 seconds plus a 5-second grace and verifies again. Even the worst inspection/list/daemon fallback path keeps this escalation below 270 seconds, well under the 4,200-second service ceiling. Every workflow lease operation uses the fixed manager, and the candidate copy must match it byte-for-byte before activation.

Pre-lease credential bootstrap checks use the read-only `validate-bootstrap` command. It does not create the adjacent rotation lock and accepts a missing nonsecret generation marker as a declared mutation requirement. Pre-lease Compose authorization uses an explicit validation-only generation value rather than rewriting the protected env. The actual `bootstrap` mutation runs only after root `prepare-run` and successful lease arm, through fixed-manager `supervise` with a 540-second outer deadline and a 510-second inner timeout.

During activation the helper keeps the timer active, takes the lease lock while atomically renaming the candidate into root-only quarantine and replacing it with a `github-runner:github-runner 0700` placeholder, copies regular single-link files into a second root-only tree, runs the stable qualifier, verifies every evidence digest and fixed component, and seals the result. The copy retains an opened quarantine-directory descriptor, performs entry lookup with directory-relative operations, and compares each opened source file's device, inode, type/mode, owner, link count, and size with the validated entry before creating its destination; observed directory, inventory, metadata, or content mutation aborts activation. Normal lease operations accept the exact deployment-owned candidate/placeholder state, while `disarm` additionally requires a root-owned `root:github-runner 0550` post-activation directory exactly bound by the root-owned current LKG pointer with exact root-owned `0440` qualification evidence, commit marker, and security marker. Recovery-only parsing also recognizes the bounded absent and sealed-but-not-yet-pointer-bound crash windows so it can restore the prior LKG. Marker contents, qualification evidence, every artifact after ownership/mode changes, the sealed directory, and both source/destination parents are fsynced before the second lease-locked rename. Only then is the root-owned pointer written, fsynced, atomically replaced, and its parent fsynced. The final sealed rename remains inside the same lease lock; the watchdog and subsequent `disarm` accept the pointer-bound sealed state without opening acceptance to an alternate directory. Leased runtime verification and rotation mutations execute through the fixed manager with a hard deadline and recurring heartbeats. On Linux, a fixed child launcher becomes a subreaper, reports readiness before work is accepted, owns the target's dedicated process group, reacts to control-pipe or parent death, and reaps every adopted descendant before reporting the original leader status. The parent never performs delayed signaling against a recycled numeric process-group ID. The effective timer contract is revalidated without an inactive interval. Quarantined source directories are retained for operator audit and must be removed only through a separately reviewed root maintenance procedure.

The watchdog service uses `ProtectSystem=strict` with the exact effective `ReadWritePaths=/home/github-runner/xframework-deploy /opt/xframework`. Atomic replacement of `xeon-dev.env` and creation of its adjacent rotation lock require write access to the parent directory; an exact file-only writable mount cannot support `os.replace`. The operator bootstrap enforces `/opt/xframework` as `root:<deployment-group>` mode `1770` and `xeon-dev.env` as `<deployment-user>:<deployment-group>` mode `0600`, both without symlinks or hard links. Bootstrap opens the validated parent and env descriptor-relatively with `O_NOFOLLOW`, validates inode/type/link/owner metadata, applies ownership and mode only through the open descriptor, and proves the path still names that inode afterward. The sticky parent lets the deployment user replace its own env and lock while preventing rename or removal of root-owned sibling evidence. The fixed root helper revalidates this exact ownership and mode contract. `/opt` outside that directory remains read-only. Forced LKG recovery explicitly starts Redis along with every non-migration Phase 0 service, even with Compose `--no-deps`, so interruption-probe termination cannot leave Redis stopped. The service uses `TimeoutStartSec=4200` (70 minutes). The bound is derived from four possible sequential 900-second stages in forced recovery (prepared-generation abort, Compose restore, runtime verification, and authenticated recovery qualification), plus up to 130 seconds for stop/inspect/kill verification and 470 seconds of controller/systemd margin. The failure SSH operation permits 4,500 seconds so it cannot terminate a valid bounded recovery first.

Required protected deployment variables:

```text
BOLT_HUB_TLS_FULLCHAIN_PATH=/absolute/path/to/fullchain.pem
BOLT_HUB_TLS_PRIVATE_KEY_PATH=/absolute/path/to/private-key.pem
BOLT_HUB_TLS_CA_PATH=/absolute/path/to/ca.crt
BOLT_HUB_EXPOSE_PORT=7000
BOLT_HUB_PUBLIC_HOSTNAME=bolt.example.internal
IDENTITYSERVER_TLS_FULLCHAIN_PATH=/absolute/path/to/identity-fullchain.pem
IDENTITYSERVER_TLS_PRIVATE_KEY_PATH=/absolute/path/to/identity-private-key.pem
IDENTITYSERVER_TLS_CA_PATH=/absolute/path/to/identity-ca.crt
BOLT_SYNTHETIC_PROXY_MODE=direct-kestrel
BOLT_PHASE0_RECOVERY_SYNTHETIC_COMMAND_PATH=/home/github-runner/xframework-deploy/hooks/run-bolt-phase0-recovery-synthetic.py
```

Phase 0 promotion accepts only `BOLT_SYNTHETIC_PROXY_MODE=direct-kestrel`; `BOLT_SYNTHETIC_PROXY_LOG_PATHS` must be absent. The manual workflow dispatch requires the operator to attest that the public route has no host reverse proxy, Tailscale Serve configuration, load balancer, or ingress. Reruns are rejected: every attempt requires a fresh dispatch, and the authorized manifest binds the GitHub actor name and stable actor ID, matching triggering actor, first attempt, workflow run, source commit, public hostname, published TLS port, exact Hub-only Compose publication, and TLS evidence. The verifier also requires every public-hostname DNS answer to identify an active non-loopback host interface and permits only an all-interface bind or an exact matched address. This trust statement assumes the protected self-hosted runner is trusted; a boundary that distrusts the runner requires a separate GitHub OIDC-signed authorization predicate. Root activation seals the result, while recovery and the steady-state watchdog reject mode drift even when no deployment lease is active, including the no-LKG/no-lease path. `logs` remains a scanner utility only and cannot qualify until the synthetic traverses the same proxy and the retained-store inventory is sealed.

## Required Inputs

1. A certificate and private key accepted by the Bolt Hub endpoint, with a SAN matching the hostname used by every Bolt client. Provisioned for xeon-dev; other environments must supply their own material.
2. A trust-distribution mechanism for every container or host connecting to the Hub, or an approved TLS terminator whose forwarded-protocol configuration is validated by the Hub. Implemented for Compose through the dedicated CA mount.
3. Access to rotate `JWT_SECRET`, `BOLT_SIGNATURE`, the Bolt Hub service identity secret, and every service identity client secret consumed by Compose.
4. Access to ASP.NET hosting, Seq, and OpenTelemetry logs for the suspected exposure window. Proxy/ingress retained-store evidence is required only when root-verified publication topology proves that layer exists; a root-sealed direct-Kestrel topology instead requires an explicit not-applicable receipt.
5. An exact captured `repository@sha256:digest` for the contained Hub and clients.
6. A dedicated revocable synthetic principal whose short-lived user and service tokens can be obtained through IdentityServer HTTPS without placing bearer values in command arguments, logs, reports, or artifacts.
7. A checked-in service-to-approved-repository map and registry-confirmed digest for every deployed service.
8. Run-scoped candidate and security-qualified last-known-good full-stack manifests that cannot be overwritten by single-service deployment.
9. A credential rotation generation ID covering JWT, Bolt signature, Hub identity, every service identity secret, and matching IdentityServer registrations.
10. Signed build provenance binding every image digest to the reviewed source commit, trusted workflow/builder identity, Dockerfile, and base-image digests.

## Execution Order

1. Freeze automatic push deployment and independent Hub/client deployment; preserve relevant application, Seq, and identity audit logs plus proxy/ingress logs when root-verified publication topology includes those layers.
2. Determine whether plaintext credentials or service-route takeover could have occurred and record the inspected sources/time range.
3. Create run-scoped candidate and last-known-good full-stack manifests. Bind every service to its approved repository, registry-confirmed exact digest, reviewed source commit, trusted workflow/builder, Dockerfile, and base-image digests through verified signed provenance.
4. Validate TLS files, resolved mount ownership, every effective Kestrel endpoint, every exact quota, one-Hub topology, and real public DNS/TLS routing without `--resolve`.
5. While Bolt is blocked, pre-stage generation `G+1` verification and service-client credentials alongside `G` without issuing/using `G+1`. After every validator and IdentityServer registration accepts both, activate `G+1` for restarted canaries, roll every service, prove runtime convergence, wait out the maximum `G` token lifetime, revoke `G`, and prove old tokens/client secrets fail. The window has a declared maximum duration and rollback uses `G+1`.
6. Run migrations, deploy only the Hub by exact digest, inspect actual listeners, reject plaintext `/bolt/ws` from a peer container, and verify trusted internal/public live and ready endpoints.
7. Deploy only IdentityServer and Communications as the canary cohort.
8. Obtain short-lived synthetic tokens through IdentityServer HTTPS. Run hostile registration, canonical generated-command RPC, transient pub/sub, publish-while-offline/reconnect/ordered durable replay/ack/no-redelivery, duplicate/out-of-order ack, Redis interruption, token expiry, and plaintext rejection.
9. Query application, Seq, and trace data using a unique synthetic token marker. Query proxy/ingress retained stores only when root-verified publication topology includes them; otherwise retain the root-sealed direct-publication not-applicable receipt. The marker must not appear in any applicable stored log or telemetry source.
10. Observe the canary for the recorded window and block promotion on authentication anomalies, reconnect storms, queue/pool growth, send-loop failure, or latency regression.
11. Deploy remaining clients in bounded batches, rerunning runtime digest, health, and synthetic gates after each batch.
12. Prove every runtime uses `G+1`, revoke `G`, and verify required HTTP and Bolt health plus rejection of old-generation credentials.
13. Exercise the security-qualified rollback manifest with `G+1`. If it cannot be applied, stop the Hub and every ingress layer present in verified publication topology, and keep Bolt unavailable.

## Required Evidence

- Certificate subject/SAN, issuer, expiration, and rotation owner; never store the private key in this repository.
- Rendered manifest and exact image digests.
- Approved repository map, registry confirmation, and run-scoped candidate/last-known-good full-stack manifests.
- Secret-rotation timestamps and secret identifiers, never secret values.
- Rotation state transitions, runtime convergence inventory, old-generation expiry/revocation result, and HTTP/Bolt health across the bounded `G`/`G+1` window.
- Verified signed source/build provenance for every deployed digest.
- Synthetic command output with timestamps and target environment.
- `/health/live` and `/health/ready` results.
- Actual listener/socket evidence, peer plaintext `/bolt/ws` rejection, real public DNS/TLS result, resolved private-key mount evidence, exact configured quotas, and exactly one running Hub.
- Queries showing the synthetic token marker is absent from every applicable log/trace store, plus either queried proxy/ingress receipts or a root-sealed direct-publication not-applicable receipt as dictated by verified topology.
- Rejection counters for plaintext, identity mismatch, quota excess, oversized frames, and disabled Media.
- Canary observation window and rollback decision.

## xeon-dev Evidence - 2026-07-12 UTC

- Root-only evidence directory: `/opt/xframework/evidence/bolt-phase0/20260712T172158Z`.
- Retained Docker logs and a live-copy Seq volume snapshot were preserved before TLS or credential changes; the evidence manifest is SHA-256 hashed.
- Precise retained-log/Seq searches found zero `access_token=` values, Authorization/Bearer combinations, JWT-shaped messages, or `ClientSecret` values. Historical exposure remains unexcluded because retention does not cover every prior deployment and no proxy/ingress archive exists.
- Dedicated CA and Hub certificate were provisioned; hostname/chain/key validation passed. The certificate expires `2026-10-10T17:26:32Z`.
- An isolated `xframework/bolt-hub:phase0-verify` image passed internal live/ready, published trusted HTTPS live/ready, absence of a published plaintext listener, and TLS 1.1 rejection. It did not exercise a WebSocket upgrade or authenticated Bolt registration. The currently deployed Hub was not restarted or replaced by that smoke test.
- Repository-side Compose preflight passed under the actual `github-runner` identity with the protected xeon-dev certificate paths.

## Local Verification - 2026-07-13

- Full `Bolt.Tests` with Redis mandatory against `xeon-dev:6379`: 303 passed, 0 failed, 0 skipped.
- PR #352 baseline IdentityServer unit tests: 21 passed. The current local correction passes 23/23 IdentityServer unit tests, including both generated HTTP adapter contracts; the IdentityServer integration project builds successfully, while its PostgreSQL/Testcontainers execution remains delegated to Linux CI. Full baseline Core tests: 195 passed.
- Full Release solution build: 0 errors. Existing repository warnings remain outside this Phase 0 change.
- `Bolt.Phase0Synthetics.Tests` passed 39 tests on Windows with one Linux-only symbolic-link test skipped. Committed Ubuntu run `29229428707` passed all 40 tests with zero skips.
- The current local tree passes 399 tests across all 19 Phase 0 Python test files on Windows with 44 platform skips; the PR #352 committed privileged Ubuntu baseline passed all then-current 370 tests with zero skips. Actionlint passed all 17 workflow YAML files; workflow parsing, six Phase 0 shell files, all 35 Phase 0 Python files, and changed-line whitespace validation pass locally. A fresh privileged Ubuntu run is required for the new tests and Linux-only branches.
- The committed CI run built the Bolt Hub container and enforced nonzero/no-skip gates for all four .NET projects and all 19 Python suites. One positive and seven negative Linux synthetic-wrapper scenarios passed.
- The root-sealed recovery, forced failure recovery, systemd override rejection, indexed-scope bypass, bounded exact Redis ACK, send-loop retirement, large-RPC quota cleanup, and public health redaction regressions are covered.
- Recovery regressions also cover strict future-heartbeat rejection, transient Docker inspection failure with a present container, exact proven container absence, daemon and empty-version failure, malformed inspection output, bounded stop/kill hangs, privileged bootstrap short reads and source races, root-owned lock replacement across the complete critical section, quarantine artifact replacement between validation and open, the exact systemd env-file write allowance, uninterrupted timer activation, explicit timer re-enablement in the pinned failure path, supervised synthetic heartbeats, and fsync-before-pointer ordering. Targeted pre-final reviews informed these regressions; a formal independent review bound to the exact final tested tree remains open because PR #352 merged without a GitHub review.

## Live Rollout Retry - 2026-07-13 UTC

- The corrected root bootstrap from PR #355 passed on xeon-dev with state `bootstrap-no-lkg-hub-stopped`; the watchdog is enabled and active, and Bolt Hub remains stopped with no deployment lease or LKG pointer.
- First-attempt workflow run `29242059314` was bound to merge `aa36cff54182ac472652564dea41f5a5af07d97d` and failed before SSH wrapper creation or remote mutation. The active `xeon-dev-deploy` runner is on `xeon-buildserver01` and runs as local user `xeon`, while the workflow incorrectly required local user `github-runner` and `/home/github-runner/.ssh/xframework_xeon_dev_ed25519`.
- The dedicated key already exists on the active runner as `xeon:xeon` mode `0600` at `/home/xeon/.ssh/xframework_xeon_dev_ed25519`; its parent `.ssh` directory is mode `0700`, it has one link, and the runner has Docker access. The correction binds the workflow to that actual local runner identity while preserving remote SSH user `github-runner`, the checked-in host key, descriptor identity checks, and bounded `ssh`/`scp` wrappers.

## Rollout Readiness Findings

- **High - Synthetic token refresh expected the wrong HTTP response shape.** Generated IdentityServer HTTP adapters return exact bare DTOs, while the refresh hook expected a `Result` envelope. The mismatch would fail live synthetics before Bolt requests executed. The local worktree now validates exact bare user-token and service-token schemas and field semantics, rejects legacy envelopes and extra or missing fields, populates the generated authentication response contract, and covers the generated HTTP adapters. This fix is not yet merged, CI-validated on the target branch, or deployed.
- **High - Direct Kestrel publication lacked an explicit proxy-proof N/A contract.** The retained proxy-log gate required paths even though xeon-dev publishes Kestrel directly, creating pressure to provide a non-evidentiary empty source. The local worktree now makes only `direct-kestrel` promotion-eligible, requires proxy log paths to be absent, rejects workflow reruns, and binds a fresh no-intermediary operator attestation to the stable actor ID, matching triggering actor, first attempt, run, commit, public hostname, active host-interface/DNS match, Compose publication, TLS port, qualification, root activation, recovery, and watchdog. This uses the existing trusted self-hosted runner threat model; distrusting that runner would require GitHub OIDC-signed authorization. The utility `logs` mode cannot qualify because its current synthetic target bypasses a proxy. The correction is not merged, CI-validated on the target branch, or deployed.

## Historical xeon-dev Readiness Audit - 2026-07-13

This pre-bootstrap snapshot is retained for audit history and is superseded by the live rollout retry section above.

The post-merge readiness remains `NO-GO`. The prerequisite repairs below changed protected host state, but no container was stopped, restarted, or replaced.

- The self-hosted `xeon-dev` runner is online as `github-runner`, Docker is reachable, and the repository-level registry secret names are present.
- The current Hub is healthy but still runs the pre-containment mutable image tag and publishes host port 7000 to container plaintext port 8080. It must remain the old deployment until the full staged gate is authorized; it is not Phase 0 evidence.
- The fixed root helper, watchdog launcher, lease manager, root-owned lock, qualifier, systemd units, restricted sudoers file, bootstrap staging tree, LKG pointer, and bootstrap-managed deployment directories are absent.
- `/home/github-runner/xframework-deploy` is still `github-runner:github-runner 0750`, and `/opt/xframework` is still `root:root 0755`; operator bootstrap must establish the documented root-owned metadata while the Hub is stopped.
- The runner-owned deployment key is provisioned at `/home/github-runner/.ssh/xframework_xeon_dev_ed25519`; strict pinned-host-key loopback SSH as `github-runner` succeeded. The active workflow validates the path, owner, mode, link count, nonempty content, and descriptor identity without printing the key.
- A distinct IdentityServer CA and server certificate are provisioned. The repository verifier passed chain, key match, hostname, permission, and validity checks. IdentityServer has not yet been restarted onto HTTPS by the Phase 0 workflow.
- The dedicated synthetic principal authenticated successfully against the currently deployed IdentityServer HTTP endpoint. This proves the principal and credentials, not the pending HTTPS or Bolt synthetic path.
- The protected env was atomically provisioned with deployment inputs and currently declares `BOLT_SYNTHETIC_PROXY_MODE=direct-kestrel`; proxy log paths are absent. The local correction binds that declaration to root-verified Hub-only TLS publication and sealed qualification evidence, but it is not authoritative on xeon-dev until merged and installed. The dedicated read-only Seq API query and Jaeger endpoints are reachable. No retained-store marker scan or complete live synthetic has run.
- The PR #352 baseline workflows, bootstrap, hooks, verifiers, tests, evidence documents, and pinned host key are committed and merged. This removes the baseline source-binding blocker, but excludes the current local P0-R21/P0-R22 changes; no signed deployment provenance, exact deployed digest set, or operator-reviewed root staging hash has been produced.
- The env parser in merged source has safe implicit types for the Phase 0 workflow inputs. The two newer rollout fixes remain local and require final-tree review and committed CI.
- The merge-triggered per-service workflows ran only shared-ownership detection; the inspected Bolt Hub, IdentityServer, and Portal `deploy` jobs were skipped. The current Hub and protected host state therefore remain pre-rollout.

## Completed Source Milestone

PR #352 committed, CI-validated, and merged every Phase 0 workflow, pinned host key, bootstrap component, hook, verifier, test, and evidence document at `fb169a530d6e80d64aeab7c73029fabea08d3152`.

## Outstanding Live Prerequisites

1. Complete and independently review the token-response and direct-publication fixes, then merge them only after committed Ubuntu CI passes.
2. Bind live build provenance and operator-reviewed root staging hashes to the exact new merge commit containing those fixes.
3. Copy that commit's bootstrap bundle into a root-only staging tree, review and hash the staged bytes, stop the Hub, and run the one-time root bootstrap. An initial LKG is not required; the stable watchdog remains active and fail-closed until the first candidate qualifies.
4. Confirm the installed metadata, fixed lock inode, enabled/active watchdog timer, and generated sudoers file with only the four documented root-helper command forms.
5. Run the complete staged workflow and retain its signed provenance, runtime, synthetic, observation, rotation, rollback, qualification, and recovery evidence.

The currently deployed Hub was not restarted, stopped, or replaced during the audit.

## Current Authorization Boundary

The operator authorized repair and retry. Non-service prerequisites were provisioned without changing a running container. The remaining boundary begins with merging the reviewed local fixes, staging their exact merged bootstrap bytes, stopping the current Hub, and running the root bootstrap. The subsequent workflow intentionally mutates Hub, canary, credentials, batched services, rollback state, qualification evidence, and forced-recovery state. Until the local fixes pass committed CI and the full workflow succeeds, Phase 0 remains `NO-GO` and Phase 1 production code remains gated.

## Failure Policy

If trusted TLS, rotation, synthetics, observation, or batched rollout fails, first re-enable and validate the fixed watchdog timer, then apply only the run-scoped security-qualified full-stack manifest using the current rotated credential generation, and finally revalidate the timer. If recovery or timer validation fails, stop the Hub and every ingress layer present in verified publication topology, and keep Bolt unavailable while retaining safe HTTP service surfaces. Fix trust, identity, configuration, or code; do not switch to `ws://`, Audit, Off, a mutable tag, old credentials, or a pre-containment image.
