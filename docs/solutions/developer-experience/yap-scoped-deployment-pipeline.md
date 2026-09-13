---
title: "Yap-scoped deployment in the normal pipeline"
date: 2026-09-13
category: developer-experience
module: XFramework
problem_type: workflow
component: development_workflow
severity: medium
status: current
applies_when:
  - "Deploying Yap or changing deployment scope and verification"
tags: [yap, deployment, ci, docker, caching]
---

# Yap-scoped deployment in the normal pipeline

The release route remains **PR → required CI → develop → Deploy Xeon Dev**.
No separate deployment command, manual container replacement, or bypass is needed.

## Selection and recovery

`scripts/deployment-scope.py` permits the fast path only when every changed file
belongs to Yap's host, client, contracts, or tests. Build metadata, appsettings,
shared/backend changes, and unknown paths require full validation/deployment.
Deployment compares against the **active deployed commit**, not merely the preceding
push, so queued, failed, or skipped releases cannot hide backend changes.

The host must have a completed release with the same protected environment-file
hash and healthy containers running the recorded immutable image IDs. Missing
evidence, drift, or a non-ancestor commit selects the full path. Eligibility is
checked again after building, before mutation; a changed baseline blocks that run.
Manual dispatch always selects a full deployment and is the repair/revalidation
path, including changes to externally managed mounted key files or ingress.

A Yap-only release builds, pushes, pulls, and restarts **only Yap**, carrying forward
the other pinned image IDs in the existing release manifest. It retains the
pre-deployment snapshot, readiness gate, atomic activation, and rollback. Its
rollback restores only Yap; failure cannot stop or recreate Bolt or other backends.
Full deployments keep migration execution/verification and MinIO initialization;
Yap-only deployments skip both because neither input can have changed.

The first deployment of this pipeline is intentionally full: it establishes the
qualification metadata required for subsequent fast releases.

## Verification

Both paths run the existing authenticated Bolt registration, authorization, RPC,
and delivery/replay checks and the credential-leak scan. Full deployments retain
disconnect-on-token-expiry verification and the 120-second core health observation,
but run the observation concurrently with smoke instead of waiting twice. Both
must succeed. Yap-only deployments skip the expiry wait and long core observation
because those images/configuration are unchanged. Tokens are still refreshed for
the leak scan, including the unused expiry marker.

After deploying Yap, both paths check its HTTPS readiness and require HTTP 401
for anonymous access to `/api/chat/conversations`. These are **not an authenticated
Yap browser/login test**: authenticated smoke exercises Bolt, while Yap's application
and client tests run in PR CI. No personal credentials or new production test account
are introduced by this change.

## Build and PR CI

Build contexts contain tracked sources for each project's transitive static project
references and linked files, plus build configuration. Unrelated application sources
no longer invalidate every publish layer. Conditional references are included;
unsupported dynamic inputs/custom targets/imports fall back to the existing full
repository context. The Dockerfile, images, registry, and bounded build concurrency
remain the same. Temporary build contexts are cleaned when the build step exits.

For Yap-only PRs, the existing required checks still complete: workflow/Compose/helper
validation and both Yap suites run, but unrelated Bolt/Storage/IdentityServer test
suites and the solution-wide build do not. Shared changes retain all suites.
NuGet publishing no longer runs for changes limited to the non-packable Yap
projects/tests. Other source changes retain package publishing.

## Validation and timing

Run `python -B scripts/deployment-pipeline-test.py` with PyYAML installed, plus the
existing boundary, diagnostic-sink, and token-refresh helper suites. Workflow CI
also parses YAML, compiles embedded Python, checks Bash syntax, and runs actionlint.

The previous measured full run spent approximately 9m07 building images and two
consecutive 2-minute verification windows. Overlapping verification removes about
2 minutes from that critical path. Yap-only savings depend on build-cache warmth;
do not quote a measured fast-deployment duration until an eligible release has run.
