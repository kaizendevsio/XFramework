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

Phase 0 containment and rollout corrections through PR [#366](https://github.com/kaizendevsio/XFramework/pull/366) are merged at `b44fe3213d2d355e3b66c3f0571af8be222e61d2`. The watchdog is enabled and active, and Bolt Hub remains stopped with no deployment lease or LKG pointer. The latest first-attempt workflow reached authenticated canary synthetics before exposing a run-scoped env-parser path defect; its stale installed recovery controller still stopped Hub but could not apply the newly merged no-LKG credential cleanup. The prepared secondary generation was subsequently removed with the exact checksum-verified run manager, and fresh validation proved current-only credential state. Phase 0 remains `Contained`, not `Verified`, until the current correction is merged, its exact bootstrap bundle is installed, and a complete staged run produces live authenticated WSS, rotation, rollback, qualification, and recovery evidence. Do not disable `RequireSecureTransport`, restore Audit mode, or deploy a pre-containment image to bypass this gate. Production promotion remains blocked until Phase 1 removes the shared service-signing trust boundary.

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
- `prepare-bound-run` submits SHA-256 bindings for the exact checked-out root helper, recovery controller, watchdog, qualifier, and systemd units together with the run identity. The helper holds the installation/deployment lock while it validates path identity and metadata, checks every digest twice, creates the bound run directory, and fsyncs a root-owned source-binding marker. Bootstrap rejects any such marker. Lease arm validates the marker and independently reopens, metadata-checks, and rehashes all six currently installed components under the same lock before it durably writes the lease, then removes and fsyncs the marker. Temporarily renaming and replaying a marker across bootstrap therefore fails on installed-byte mismatch, and the transition never exposes an authorized bootstrap gap. A missing command, malformed request, installed-byte mismatch, invalid marker, or concurrent operator bootstrap fails before credential or service mutation; only the operator bootstrap can update those files.
- The watchdog timer remains enabled and active throughout candidate quarantine, qualification, sealing, and pointer publication. The root helper uses the deployment lease lock only for the two candidate-path swaps, and the pinned failure handler invokes the fixed `ensure-watchdog` command before and after forced recovery.

## Root Bootstrap and Fixed-Component Updates

The workflow cannot bootstrap or update its own root trust boundary, and the root bootstrap refuses an ordinary `github-runner` checkout. Before execution, an operator must extract only the approved merge commit's Git blobs into a temporary source tree, copy that exact bundle into a root-only staging tree, and compare every staged hash back to the Git object. Repeat this procedure before the next deployment whenever any installed root helper, watchdog, lease manager, qualifier, service, or timer source changes; `prepare-bound-run` rejects an older installation. Bootstrap is allowed only with dispatch frozen, Hub stopped, and the deployment lease, LKG pointer, and every prepared source-binding marker absent. Once an LKG exists, fixed-component replacement requires a separate reviewed maintenance procedure rather than invalidating active recovery trust.

**Known P2 maintenance blocker:** the post-LKG fixed-component replacement procedure is not implemented. After the first qualified LKG is published, do not remove or rename its pointer and do not bootstrap changed fixed components. A future reviewed maintenance slice must provide an outage-bound suspend/update/requalify/rollback state machine that preserves a usable old recovery bundle until a replacement LKG is qualified. This does not block the current first qualification because xeon-dev has no LKG, but it blocks every later root-helper, watchdog, lease-manager, qualifier, or unit update and remains an explicit follow-on issue.

```bash
set -euo pipefail
commit=<approved-40-character-merge-sha>
stage="/root/xframework-bolt-phase0-bootstrap-$commit"
source_root="$(/usr/bin/mktemp -d)"
trap '/usr/bin/rm -rf -- "$source_root"' EXIT
bundle=(
  deploy/bootstrap-xframework-bolt-phase0-root.sh
  deploy/systemd/xframework-bolt-phase0-watchdog.service
  deploy/systemd/xframework-bolt-phase0-watchdog.timer
  scripts/manage-bolt-phase0-root.py
  scripts/run-bolt-phase0-watchdog.sh
  scripts/manage-bolt-phase0-deployment-lease.py
  scripts/verify-bolt-phase0-qualification.py
)

/usr/bin/git fetch --no-tags origin develop
test "$(/usr/bin/git rev-parse "$commit^{commit}")" = "$commit"
/usr/bin/git merge-base --is-ancestor "$commit" origin/develop
/usr/bin/git archive --format=tar "$commit" -- "${bundle[@]}" \
  | /usr/bin/tar -xf - -C "$source_root"

! /usr/bin/sudo /usr/bin/test -e "$stage"
! /usr/bin/sudo /usr/bin/test -L "$stage"
/usr/bin/sudo /usr/bin/install -d -o root -g root -m 0700 \
  "$stage/deploy/systemd" "$stage/scripts"
for path in "${bundle[@]}"; do
  /usr/bin/sudo /usr/bin/cp --no-dereference -- "$source_root/$path" "$stage/$path"
done
/usr/bin/sudo /usr/bin/chown -R root:root "$stage"
/usr/bin/sudo /usr/bin/find "$stage" -xdev -type d -exec /usr/bin/chmod 0700 {} +
/usr/bin/sudo /usr/bin/find "$stage" -xdev -type f -exec /usr/bin/chmod 0400 {} +
/usr/bin/sudo /usr/bin/chmod 0500 "$stage/deploy/bootstrap-xframework-bolt-phase0-root.sh"
test -z "$(/usr/bin/sudo /usr/bin/find "$stage" -xdev -type l -print -quit)"

for path in "${bundle[@]}"; do
  expected="$(/usr/bin/git cat-file blob "$commit:$path" | /usr/bin/sha256sum | /usr/bin/awk '{print $1}')"
  actual="$(/usr/bin/sudo /usr/bin/sha256sum "$stage/$path" | /usr/bin/awk '{print $1}')"
  test "$actual" = "$expected"
  printf '%s  %s\n' "$actual" "$path"
done | /usr/bin/sudo /usr/bin/tee "$stage.reviewed.sha256"
/usr/bin/sudo /usr/bin/chmod 0400 "$stage.reviewed.sha256"

! /usr/bin/sudo /usr/bin/test -e /home/github-runner/xframework-deploy/phase0-watchdog/deployment-lease.json
! /usr/bin/sudo /usr/bin/test -L /home/github-runner/xframework-deploy/phase0-watchdog/deployment-lease.json
! /usr/bin/sudo /usr/bin/test -e /home/github-runner/xframework-deploy/phase0-last-known-good/current
! /usr/bin/sudo /usr/bin/test -L /home/github-runner/xframework-deploy/phase0-last-known-good/current

# Stop xframework-bolt-hub, then execute only the absolute staged bootstrap.
/usr/bin/sudo /usr/bin/env -i PATH=/usr/sbin:/usr/bin:/sbin:/bin \
  /usr/bin/bash "$stage/deploy/bootstrap-xframework-bolt-phase0-root.sh" "$stage"
```

The bootstrap requires itself to be the exact absolute file under the supplied staging root. It opens every staging path component descriptor-relatively and rejects a non-root owner, group/world-writable parent, symlink, non-regular component, hard link, or any writable component. Companion bytes are copied through `O_NOFOLLOW` descriptors, read completely to EOF under a 4 MiB ceiling despite short reads or `EINTR`, checked twice for exact size, stable identity, metadata, and content, then atomically installed without following a source pathname. The bootstrap refuses to proceed while `xframework-bolt-hub` is running. A nonzero container inspection is accepted as an absent container only when a separate bounded `docker container ls -a --no-trunc --filter name=^/xframework-bolt-hub$ --format '{{.Names}}'` returns successfully with exactly no names and a bounded `docker info --format '{{.ServerVersion}}'` returns a nonempty version. A present named container, daemon, socket, permission, empty-version, malformed-state, timeout, or other inspection failure aborts bootstrap. It then takes the fixed deployment lock with a 30-second bound, rejects any lease, LKG pointer, or `bootstrap-source-binding.json` entry in a prepared run, and holds the lock until all fixed files, sudoers policy, and systemd state are installed. The lock is released immediately before final root-helper self-validation to avoid recursive acquisition; dispatch must remain frozen through that final check. The root helper and no-LKG watchdog branch enforce the same stopped-Hub proof. It installs these fixed root-owned files:

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

The fixed lease manager owns the external deployment lease and failure controller. The root helper owns candidate preparation and activation. Both open the same pre-created `root:github-runner 0440` deployment lock read-only with `O_NOFOLLOW`; its complete parent chain is root-owned and nonwritable. They verify descriptor, directory-entry, pathname, parent, and inode identity before and after `flock` and again before unlock. The deployment user cannot create, rename, unlink, or replace this lock, including while either critical section is active. Bootstrap reruns preserve its inode. The protected self-hosted runner remains part of the deployment trust boundary because its account owns lease state and has Docker access; these controls fail closed for stale or mixed-version installed controllers, but they do not claim to contain a malicious runner account.

The generated `/etc/sudoers.d/xframework-bolt-phase0-root` permits only these exact no-wildcard command forms:

```text
/usr/local/sbin/xframework-bolt-phase0-root verify-bootstrap
/usr/local/sbin/xframework-bolt-phase0-root ensure-watchdog
/usr/local/sbin/xframework-bolt-phase0-root prepare-bound-run
/usr/local/sbin/xframework-bolt-phase0-root activate
```

sudo-rs 0.2.8 does not support wildcards in command arguments, so dynamic run IDs cannot be expressed safely in the sudoers entry. `prepare-bound-run` and `activate` instead accept canonical exact-schema JSON requests on stdin, capped at 2 KiB with a five-second read deadline. The bound-run request contains the run identity and exactly six lowercase SHA-256 digests for the checked-out bootstrap sources. The helper takes the same lock used by bootstrap and lease operations, validates every opened file against its pathname after reading, checks the complete installed bundle twice, creates the run directory, and writes a `root:<deployment-group> 0440`, single-link marker containing the exact run identity and six nonsecret digests before releasing the lock. The deployment user can read but cannot forge that marker. Production `arm` requires it and validates its schema, content, owner, group, mode, link count, size, descriptor/path identity, and stability under the same lock. It then independently opens all six installed fixed paths with `O_NOFOLLOW`, requires exact root ownership, group, mode, link count, bounded size, and stable descriptor/path identity, and compares their SHA-256 digests to the marker. Only then does it persist and fsync a v2 lease carrying `bootstrap_source_bound=true` before unlinking and fsyncing the marker directory. The current manager rejects production arm calls that omit binding, and the current watchdog and activation helper reject legacy v1 leases, missing binding state, and v2 leases marked unbound. This makes retained old-manager/current-controller combinations fail closed. A deployment user that renames the marker away for bootstrap and later restores it cannot replay it against the changed installation. A crash before lease persistence leaves the marker blocking bootstrap; a crash after persistence leaves the lease, or both states, blocking bootstrap. The helper rejects malformed UTF-8, duplicate/missing/extra fields, non-string values, noncanonical encoding, trailing data, malformed digests, invalid identities, attempts other than `1`, unexpected commits, and projects other than `xframework`; the root boundary is initialized before this read so every malformed, oversized, or timed-out privileged request invokes the immediate Hub stop path. No interpreter, shell, file utility, or `systemctl` command is allowed through sudo. The fixed helper's non-abbreviating parser exposes exactly five reviewed commands and rejects positional arguments; the generated `github-runner` sudoers policy delegates only the four deployment commands and denies operator-only `abandon-bound-run`. Privileged CI executes that policy behavior under both traditional sudo and the checksum-pinned sudo-rs 0.2.8 binary, proving exact positive forms, positional/unknown/bare/operator-command denial, non-PTY stdin preservation, and EOF propagation. Contract tests fail if another parser command is added without a corresponding security review. With no LKG pointer, bootstrap is valid only while the Hub is verified stopped. During the first rollout, the stable watchdog permits a running Hub only while the fixed root-owned lease manager validates a fresh, schema-complete lease bound to the prepared run directory; an absent, stale, any-future-dated, malformed, wrongly owned, or incorrectly permissioned lease stops Hub. Its fail-closed path bounds stop at 40 seconds plus a 5-second kill grace, always verifies state, then bounds kill at 10 seconds plus a 5-second grace and verifies again. Even the worst inspection/list/daemon fallback path keeps this escalation below 270 seconds, well under the 4,200-second service ceiling. Every workflow lease operation uses the fixed manager, and the candidate copy must match it byte-for-byte before activation.

Pre-lease credential bootstrap checks use the read-only `validate-bootstrap` command. It does not create the adjacent rotation lock and accepts a missing nonsecret generation marker as a declared mutation requirement. The build controller validates the candidate Compose, pins, and provenance without claiming deployment authority. The xeon-dev deployment host repeats those checks and exclusively authorizes publication after binding every public DNS answer to its own active interfaces, before candidate image pulls. Both use an explicit validation-only generation value rather than rewriting the protected env. The actual `bootstrap` mutation runs only after root `prepare-bound-run` and marker-required lease arm, through fixed-manager `supervise` with a 540-second outer deadline and a 510-second inner timeout. If a workflow terminates after `prepare-bound-run` but before lease arm, the marker is intentionally orphaned and bootstrap remains blocked. The installed root helper provides operator-only `abandon-bound-run`; it is deliberately absent from the `github-runner` sudoers alias. Freeze dispatches, prove there is no active or queued workflow, and generate its exact request from the approved installed commit:

```bash
set -euo pipefail
commit=<approved-40-character-merge-sha>
run_id=<abandoned-first-attempt-run-id>
request="$(/usr/bin/mktemp)"
trap '/usr/bin/rm -f -- "$request"' EXIT
/usr/bin/python3 - "$commit" "$run_id" >"$request" <<'PY'
import hashlib
import json
import subprocess
import sys

commit, run_id = sys.argv[1:]
sources = {
    "root_helper_sha256": "scripts/manage-bolt-phase0-root.py",
    "watchdog_sha256": "scripts/run-bolt-phase0-watchdog.sh",
    "lease_manager_sha256": "scripts/manage-bolt-phase0-deployment-lease.py",
    "qualifier_sha256": "scripts/verify-bolt-phase0-qualification.py",
    "service_fragment_sha256": "deploy/systemd/xframework-bolt-phase0-watchdog.service",
    "timer_fragment_sha256": "deploy/systemd/xframework-bolt-phase0-watchdog.timer",
}
request = {"run_id": run_id, "run_attempt": "1"}
for field, path in sources.items():
    raw = subprocess.run(
        ["/usr/bin/git", "cat-file", "blob", f"{commit}:{path}"],
        check=True,
        capture_output=True,
    ).stdout
    request[field] = "sha256:" + hashlib.sha256(raw).hexdigest()
print(json.dumps(request, sort_keys=True, separators=(",", ":")))
PY
/usr/bin/sudo /usr/local/sbin/xframework-bolt-phase0-root abandon-bound-run <"$request"
```

The Linux-only command acquires the fixed deployment lock, rejects any lease or LKG, revalidates stopped-Hub bootstrap state and all six installed hashes, requires the exact deployment-owned run directory and root-owned group-readable marker content, then opens and temporarily changes that directory to root-only mode before revalidating and unlinking the marker. It assumes restoration responsibility before the first ownership mutation, fsyncs the directory, restores the exact deployment ownership and mode through the open descriptor on both success and failure, verifies pathname identity, and fsyncs both directory levels. The deployment account therefore cannot swap the authenticated marker during abandonment. Non-POSIX invocation fails closed. The command leaves the abandoned run and all other evidence intact. Never invoke it merely to make bootstrap proceed.

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

Phase 0 promotion accepts only `BOLT_SYNTHETIC_PROXY_MODE=direct-kestrel`; `BOLT_SYNTHETIC_PROXY_LOG_PATHS` must be absent. The manual workflow dispatch requires the operator to attest that the public route has no host reverse proxy, Tailscale Serve configuration, load balancer, or ingress. Reruns are rejected: every attempt requires a fresh dispatch, and the authorized manifest binds the GitHub actor name and stable actor ID, matching triggering actor, first attempt, workflow run, source commit, public hostname, published TLS port, exact Hub-only Compose publication, and TLS evidence. Only the xeon-dev deployment-host verifier may authorize the manifest; it requires every public-hostname DNS answer to identify one of its own active non-loopback interfaces and permits only an all-interface bind or an exact matched address. The build controller performs no DNS or interface topology claim and cannot emit deployment authorization. This trust statement assumes the protected self-hosted runner is trusted; a boundary that distrusts the runner requires a separate GitHub OIDC-signed authorization predicate. Root activation seals the result, while recovery and the steady-state watchdog reject mode drift even when no deployment lease is active, including the no-LKG/no-lease path. `logs` remains a scanner utility only and cannot qualify until the synthetic traverses the same proxy and the retained-store inventory is sealed.

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
- PR #365 passed 446 tests across all 19 Phase 0 Python test files on Windows with 49 platform skips. PR #366 passed 464 tests across the same 19 files with the same 49 expected platform skips. The PR #367 parser/source-binding/bootstrap correction passed 489 tests locally with 62 Windows platform skips and all 489 tests in a fresh privileged Linux replay with zero skips. The PR #368 protected-env grammar correction passed 491 tests locally with the same 62 expected Windows platform skips and all 491 tests in committed privileged Linux CI with zero skips. The current strict-TLS correction passes 494 tests locally with 63 expected Windows platform skips; its POSIX certificate-generation fixture separately passes on xeon-dev Linux and proves both malformed-CA rejection and compliant-CA acceptance. Committed CI must reproduce the complete zero-skip privileged Linux replay plus checksum-verified actionlint v1.7.8, all workflow YAML parses, six shell-file syntax checks, and the checksum-pinned sudo-rs 0.2.8 policy contract before merge.
- The committed CI run built the Bolt Hub container and enforced nonzero/no-skip gates for all four .NET projects and all 19 Python suites. One positive and seven negative Linux synthetic-wrapper scenarios passed.
- The root-sealed recovery, forced failure recovery, systemd override rejection, indexed-scope bypass, bounded exact Redis ACK, send-loop retirement, large-RPC quota cleanup, and public health redaction regressions are covered.
- Recovery regressions also cover strict future-heartbeat rejection, transient Docker inspection failure with a present container, exact proven container absence, daemon and empty-version failure, malformed inspection output, bounded stop/kill hangs, privileged bootstrap short reads and source races, root-owned lock replacement across the complete critical section, quarantine artifact replacement between validation and open, the exact systemd env-file write allowance, uninterrupted timer activation, explicit timer re-enablement in the pinned failure path, supervised synthetic heartbeats, and fsync-before-pointer ordering. Targeted pre-final reviews informed these regressions; a formal independent review bound to the exact final tested tree remains open because PR #352 merged without a GitHub review.

## Live Rollout Retry - 2026-07-13 UTC

- The corrected root bootstrap from PR #355 passed on xeon-dev with state `bootstrap-no-lkg-hub-stopped`; the watchdog is enabled and active, and Bolt Hub remains stopped with no deployment lease or LKG pointer.
- First-attempt workflow run `29242059314` was bound to merge `aa36cff54182ac472652564dea41f5a5af07d97d` and failed before SSH wrapper creation or remote mutation. The active `xeon-dev-deploy` runner is on `xeon-buildserver01` and runs as local user `xeon`, while the workflow incorrectly required local user `github-runner` and `/home/github-runner/.ssh/xframework_xeon_dev_ed25519`.
- The dedicated key already exists on the active runner as `xeon:xeon` mode `0600` at `/home/xeon/.ssh/xframework_xeon_dev_ed25519`; its parent `.ssh` directory is mode `0700`, it has one link, and the runner has Docker access. The correction binds the workflow to that actual local runner identity while preserving remote SSH user `github-runner`, the checked-in host key, descriptor identity checks, and bounded `ssh`/`scp` wrappers.
- First-attempt workflow run `29243531605` passed the corrected SSH and host preflight, then failed before protected-env validation, lease arming, or deployment because the local Compose verifier was given six nonexistent `/tmp/bolt-phase0-validation` TLS paths. Built-in recovery confirmed `no-active-lease-no-lkg-hub-stopped`.
- The correction creates six distinct, empty, nonsecret, mode-`0600` regular files under the run-specific `RUNNER_TEMP` solely for local Compose secret-alias and mount-ownership checks. The later xeon-dev verifier remains bound to the protected env, real TLS files, TLS chain/key validation, and actual file identities.
- First-attempt workflow run `29244847846` passed runner, SSH, local Compose, and protected-input validation, then failed at `prepare-run` before lease acquisition or service mutation. The installed sudo-rs 0.2.8 policy accepted `verify-bootstrap` but denied dynamic arguments because that release does not support command-argument wildcards. Recovery evidence recorded `no-active-lease-no-lkg-hub-stopped` with `hub_stopped=true`.
- The correction keeps four exact no-wildcard sudo forms and transfers dynamic fields through a bounded canonical stdin request. Invalid privileged input invokes the immediate fail-closed Hub stop path, and activation evidence is captured in a random mode-`0600` runner-local file rather than a predictable path in a deployment-writable remote directory. Privileged CI behavior-tests the policy under traditional sudo and checksum-pinned sudo-rs 0.2.8. This preserves the existing argument/path validation without delegating arbitrary helper arguments, an interpreter, shell, file utility, or service manager.
- First-attempt workflow run `29248685927` passed the corrected sudo-rs bootstrap verification and `prepare-run`, then failed while installing synthetic hooks because `verify-bolt-phase0-env.py` applied selected typed-value character restrictions to every env record. A legitimate opaque `DB_PASSWORD` therefore blocked an unrelated absolute-path read. Recovery evidence again recorded `no-active-lease-no-lkg-hub-stopped` with `hub_stopped=true`.
- The correction parses every env record as inert UTF-8 `NAME=value` data without evaluation. The CLI retains and returns only requested values; trusted short-lived module callers can still request the complete inert map for their existing workflows. Duplicate names, malformed records, NULs, and embedded line breaks remain rejected globally; strict safe-character and hostname/path/port validation is applied only to requested typed values. Tests prove shell-significant opaque secrets neither block a typed read nor appear in stdout or diagnostics.
- First-attempt workflow run `29250662582` passed the corrected env parser, protected input validation, root bootstrap verification, hook installation, and credential bootstrap validation, then failed before image build, lease acquisition, or service mutation. The workflow attempted to upload a candidate Compose file directly beneath `/home/github-runner/xframework-deploy`, which the root helper intentionally owns as `root:root 0755`. Recovery evidence recorded `no-active-lease-no-lkg-hub-stopped`, `hub_stopped=true`, and no lease.
- The correction preserves the root-owned deployment and `runs` parents and stages transient candidate files and verifiers only inside the prepared `github-runner:github-runner 0700` run directory. The mutable rotation state is bound to that lease-owned run path; the fixed recovery controller derives it from the validated lease instead of accepting a caller-selected path, and the workflow removes it after finalized rotation evidence is captured. Transient tools and preflight receipts are removed before activation, disarm/recovery receipts are captured directly into runner-local evidence, and qualification rejects any artifact outside its exact digest-bound inventory. Static workflow coverage permits only the bootstrap-managed `hooks` child beneath `REMOTE_DEPLOY_DIR` and rejects runner attempts to recreate privileged directory boundaries.
- First-attempt workflow run `29255530535` passed the corrected root/run staging boundary, actual xeon-dev TLS and manifest validation, and registry login, then failed before image construction, lease acquisition, or service mutation. The build-input evidence rendered Compose without profiles, so Compose omitted the intentionally profiled `bolt-phase0-synthetics` service and the exact build inventory failed with `Compose build-input coverage is incomplete`. The failure-recovery step also rejected its valid empty `mktemp` receipt because GNU `stat %F` describes it as `regular empty file`; the watchdog independently kept Hub stopped with no lease or LKG.
- The correction renders build inputs with all Compose profiles, explicitly activates `phase0-verification` for the build, and retains the exact nonsecret build-service contract. Runner-local activation, disarm, and recovery receipts are atomically created at their final paths with Bash `noclobber`; SSH and fallback output is written through already-open file descriptors. The workflow validates descriptor/path device and inode identity plus exact UID, mode `0600`, link count, regular-file type, and non-symlink status before and after writes. Failure cleanup closes the descriptor but performs no pathname deletion, avoiding a validation-to-unlink replacement race; bound private receipts remain available for job cleanup or evidence upload. Static workflow coverage rejects the profile-less render, pathname-based temporary receipts, pathname unlink cleanup, and the empty-file-sensitive metadata contract.
- First-attempt workflow run `29258255887` passed the corrected all-profile build-input gate, built all 14 images, and pushed their digest-pinned manifests for merge `4d705c2598f9698f721516e357d2358402a06d2c`. It then failed before provenance authorization, candidate pull, lease acquisition, or service mutation because the pinned Cosign installer defaulted to v2.4.3, which does not support the `--new-bundle-format` flag used by the workflow. The SHA-256-verified workflow artifact recorded 14 exact image pins and passed recovery with `no-active-lease-no-lkg-hub-stopped`; an independent strict host check confirmed no lease or LKG, an active watchdog timer, a stopped Hub, and no listener on port 7000.
- The correction pins signed `cosign-installer` v4.1.2 commit `6f9f17788090df1f26f669e9d70d6ae9567deba6` and Cosign v3.1.1 explicitly. A new pre-build gate verifies the exact binary version and every signing/verification CLI option consumed later by the workflow, so an installer/command mismatch fails before the expensive image build. Cosign v3.1.1 uses the standardized bundle format by default; it deprecates and hides the explicit format selector from help while retaining it for v3 compatibility. The workflow keeps that explicit selector, and the pre-build gate invokes both hidden parser forms to reproduce the exact v2.4.3 failure condition without signing or requesting OIDC credentials. Static coverage locks the installer commit, binary version, gate ordering, required CLI surface, compatibility invocations, and provenance syntax.
- First-attempt workflow run `29261219986` passed the v3.1.1 installer and CLI contract, generated keyless attestations for all 14 digest-pinned images, and verified every attestation against the GitHub Actions OIDC identity, transparency log, and trusted certificate authorities. The retained-evidence verifier then failed before provenance authorization, candidate pull, lease acquisition, or service mutation because it required the combined builder and workflow allowlists to be globally unique even though the workflow intentionally uses the same canonical GitHub workflow URL for both roles. The SHA-256-verified artifact retained all 14 predicates, bundles, and verification documents; recovery passed with `no-active-lease-no-lkg-hub-stopped`, and an independent strict host check confirmed the same contained state.
- The correction preserves nonempty, safe, duplicate-free builder and workflow allowlists but validates uniqueness within each namespace. The same trusted identity may therefore represent both the builder and workflow without permitting duplicate entries inside either allowlist. Artifact replay also exposed that Cosign v3.1.1 deliberately wraps SLSA provenance v1 in in-toto Statement v0.1; this gate pins that exact producer contract and rejects Statement v1 for the pinned producer without claiming Statement v1 is invalid generally. Regression coverage proves the production overlap passes, duplicates and unsafe values in each namespace fail closed, and a mismatched statement version fails; the retained 14-image live artifact replays with 14 verified bindings and zero errors through the corrected verifier.
- First-attempt workflow run `29263288260` passed the corrected provenance verifier for all 14 images, then failed before candidate pull, lease acquisition, migration, or service mutation because the local build controller was required to own the deployment hostname. The controller has Tailscale address `100.105.210.20`, while `xeon-dev.tailed40e.ts.net` correctly resolves to the remote deployment host at `100.75.11.49`; the authoritative remote verifier had not yet run. Artifact `8284359913` independently matched GitHub SHA-256 `debdfc56b0c38a863d097a313398b985bfda666f9c84b0634da58f79a90f0517`, retained all signed provenance, and recorded passed recovery with `no-active-lease-no-lkg-hub-stopped`. A strict post-run host check confirmed the watchdog enabled and active, no lease or LKG, Hub stopped, and no port 7000 listener.
- The correction gives the local build controller and remote deployment host explicit publication-check contexts. The controller validates the static candidate Compose plus pins and provenance, performs no deployment-host DNS/interface check or topology claim, and cannot emit `deployment_authorized: true`. The remote xeon-dev verifier remains authoritative: it repeats the artifact checks, validates the first-attempt operator attestation, resolves every published address onto an active local interface, and matches any explicit binding before it can emit `deployment_authorized: true`. The workflow parses both receipts, and remote authorization completes before any candidate image pull, credential/runtime mutation, migration, or service mutation.
- First-attempt workflow run `29266243859` was bound to merge `d0ad8050f1ed12561a82ea536c7100faca412cb4`. It passed local and authoritative remote publication authorization, built and provenance-verified all 14 images, pulled the candidate pins, armed the watchdog lease, prepared credential generation G+1, completed migration, deployed Hub, and passed the dedicated Hub TLS/plaintext check. Staged runtime verification then failed because Docker's formatted inspect rejected the absent `State.Health` map on the successful one-shot migration container, the verifier counted Docker's configured `127.0.0.11` embedded-DNS TCP socket as an application listener, and it rejected the unbound `8080/tcp: null` entry that Docker retains beside the sole bound `8443/tcp` runtime publication.
- Artifact `8285581091` independently matched GitHub SHA-256 `625956c32737894096b71770ddf8daddeea3aa56722fba6d489f4f0472a02d3c` and retained the passed local/deployment-host authorization receipts plus the failed runtime and recovery evidence. Recovery reported `no-qualified-lkg-after-mutation` and returned nonzero, but its fail-closed action stopped Hub. A strict host check confirmed the watchdog timer enabled and active, no lease or LKG pointer, the candidate Hub stopped, and no listener on host port 7000; named data volumes were not changed.
- PR #365 merged the indexed optional-health, exact listener ownership, Docker DNS, and runtime publication correction as `736e1f945a0480c316ccc1ce944c1b40eb8d8ed4`. Its committed CI passed, including all Phase 0 suites and independent final review.
- Fresh first-attempt workflow run `29269040262` was bound to that merge and passed authorization, SSH, Compose, protected-input, watchdog-bootstrap, and synthetic-hook gates. It then failed before image build, lease acquisition, migration, or service mutation because the complete prepared secondary credential generation from run `29266243859` remained in the protected env after no-LKG recovery had stopped Hub and deleted the lease without invoking `abort-prepared`. Recovery again proved Hub stopped, no port 7000 listener, no lease/LKG, and an active watchdog; artifact `8286639449` independently matched GitHub SHA-256 `80e90ad9d4b53958a6d9208d2b57968b79cda96b276575994cb338db1e9daf51`.
- With no active or queued deployment, no lease, Hub stopped, port 7000 closed, and the originating run manager checksum-matched to its merged source, the operator-authorized repair invoked that exact manager's `abort-prepared`. The owner-only receipt reported an aborted unactivated rotation, the old state file was removed, and a fresh bootstrap validation reported `mutation_required: false`. The recovery correction now invokes the lease-bound run manager after verified Hub shutdown and before lease removal even when service mutation has begun. A failed or phase-ineligible abort retains the lease and keeps Hub stopped, preserving a recoverable fail-closed pointer instead of orphaning secondary state. Missing journals are accepted only after locked current-only env validation; orphaned secondary fields fail closed in no-LKG, qualified-LKG, forced, and lease-less forced recovery, and no restore is attempted. When both lease and LKG are absent, there is no trusted rotation manager to prove credential shape, so recovery verifies Hub shutdown but returns `credential-state-unverified` instead of claiming success.
- PR #366 merged that recovery correction as `b44fe3213d2d355e3b66c3f0571af8be222e61d2` after committed CI and independent final review. Fresh first-attempt workflow run [29272901969](https://github.com/kaizendevsio/XFramework/actions/runs/29272901969) passed authorization, protected input validation, both provenance gates, all 14 image builds and pulls, lease arm, G+1 preparation, migration, Hub TLS/plaintext and staged-runtime checks, IdentityServer/Communications canary deployment, readiness, and staged canary runtime. It failed before observation and broader promotion because `run-bolt-phase0-synthetics.sh` reconstructed `/home/github-runner/xframework-deploy/verify-bolt-phase0-env.py` instead of using the verifier staged in its run directory. Artifact `8288262631` independently matched GitHub SHA-256 `e372e493e444f7e02da2c4f6d4bd4386a0d73b1429bf2b3d74c91e56747b1829`.
- Failure recovery used the still-installed pre-PR #366 fixed controller: it stopped Hub and removed the lease but reported the older no-LKG outcome without aborting the prepared generation. With no active or queued deployment, no lease/LKG, Hub stopped, port 7000 closed, and the run manager checksum exactly matching merged source, the operator-authorized cleanup invoked that run manager's `abort-prepared`. The state file was removed and a fresh read-only validation reported `mutation_required: false`; named volumes were not changed. The current correction passes the explicit run-scoped `REMOTE_ENV_PARSER` into every synthetic and adds atomic `prepare-bound-run` source binding for all six installed bootstrap components. The next retry is forbidden until the correction is merged and that exact merged bootstrap bundle is installed through the root-only operator procedure above.
- PR #367 merged that correction as `be24146acb7930b0f9f543fd7b5f80614bc3969f`; the exact six-component bootstrap bundle was reviewed, hash-bound, and installed from a root-only staging tree. Fresh first-attempt workflow run [29282982402](https://github.com/kaizendevsio/XFramework/actions/runs/29282982402) then passed bootstrap source binding, provenance, all image builds and pulls, lease arm, G+1 preparation, migration, Hub TLS/plaintext/runtime checks, and IdentityServer/Communications canary readiness/runtime. It failed before the first Bolt RPC because the token-refresh hook parsed the complete protected env with an uppercase-only key grammar and rejected the valid .NET configuration key `ControlPanel__BootstrapAdmin__Password` as `BOLT_PHASE0_REFRESH_ENV_SYNTAX`. A secret-free structural diagnostic confirmed no BOM, NUL, bare carriage return, duplicate, or value-syntax failure and identified that key grammar as the only rejection.
- Recovery stopped Hub, removed the lease and prepared rotation state, and left no LKG, bootstrap source marker, rotation journal, or port 7000 listener; IdentityServer and Communications remained healthy and named volumes were preserved. Artifact `8292139393` independently matched GitHub SHA-256 `8b143bbe4343dab7f5a4443f9709e3d1bed3ce11e5cfe213d55b2ac44f0a7645`. The current correction aligns the refresh hook with the existing protected-input and typed-env Compose identifier grammar, including mixed-case and leading-underscore keys, and regression-tests the exact .NET key. It does not relax value, encoding, file-integrity, permission, or secret-redaction checks and changes none of the six fixed root bootstrap components, so the merged mutable hook can be installed by a fresh first-attempt workflow without another root bootstrap installation.
- PR #368 merged the protected-env grammar correction as `e5bba590948b2ec0625dfd2e41815624ae594d2e` after all first-attempt checks passed, including the 491-test privileged Linux gate with zero skips. Fresh first-attempt workflow run [29285153991](https://github.com/kaizendevsio/XFramework/actions/runs/29285153991) passed the corrected hook installation and protected-env grammar, provenance, image pulls, lease arm, G+1 preparation, migration, Hub TLS/plaintext/runtime, and canary readiness/runtime. The refresh hook then failed before the first Bolt RPC with the fixed secret-free code `BOLT_PHASE0_REFRESH_TLS_CONNECTION`.
- A same-identity, read-only TLS replay isolated OpenSSL verification code 92: the provisioned IdentityServer CA has critical `CA:TRUE` basic constraints but no key-usage extension, so Python 3.13/OpenSSL 3.5 strict verification correctly rejects it as `CA cert does not include key usage extension`. The existing shell preflight used non-strict `openssl verify` and accepted the malformed chain. Recovery stopped Hub and removed the lease and prepared rotation state, leaving no LKG, source marker, rotation journal, or port 7000 listener; IdentityServer and Communications remained healthy and named volumes were preserved. Artifact `8292954311` is bound to GitHub SHA-256 `cdee729c7e7c851d69be4e3099ce516ef0f79448f989d07f85f1768b6888c19a`.
- The current correction requires `openssl verify -x509_strict` for both internal and published hostnames in both Hub and IdentityServer TLS preflights. Behavioral fixtures prove a CA without signing key usage fails while a CA with critical `keyCertSign,cRLSign` passes. Before another fresh workflow attempt, the protected IdentityServer CA/server full chain must be reissued with critical CA basic constraints and critical certificate-signing/CRL-signing key usage, then pass the corrected verifier and the Python TLS handshake. Weakening the refresh client's strict verification is forbidden.

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
