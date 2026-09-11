---
title: "Isolate IdentityServer validation rate budgets"
date: 2026-09-11
category: workflow-issues
module: IdentityServer, Bolt
problem_type: rate-limiting
component: authentication
severity: high
applies_when:
  - "Bolt subscriptions fail to bind while IdentityServer returns HTTP 429"
tags: [identityserver, bolt, authentication, rate-limiting]
status: current
---

# Isolate IdentityServer validation rate budgets

## Symptom and evidence

During a three-user Yap test, typing initially reached all other members, then
stopped after the service transport renewed. Hub logs showed the resubscription
frames arriving but being rejected as unauthorized. Fresh subscriptions later
failed too; this was not a missing client rebind. Concurrent Hub logs showed
IdentityServer signing-key/generation-policy requests failing with HTTP 429.

`IdentityServerHttpActorIdentityProvider` validates each subscription through
`POST /api/auth/validate-session` and fails closed when that dependency is
unavailable. The previous global IP partition allowed only 100 HTTP requests/minute
across every route. A Hub multiplexing users and service discovery could exhaust
that shared budget, starving session checks and credential-policy refreshes.

## Fix

The global limiter still applies to every request, but uses three **fixed** route
classes per remote IP:

| Class | Exact POST route | Requests/minute |
|---|---|---:|
| Session validation | `/api/auth/validate-session` | 600 |
| Public signing-key queries | `/api/service-identity/signing-keys/query` | 60 |
| General | All other routes/methods | 100 |

Ten session checks/second accommodates the observed shared-Hub workload without
letting it spend the login or signing-key budgets. One signing-key query/second
leaves room for the normal five-second credential-policy refresh plus bursts.
The endpoint's existing actor/session/service validation is unchanged. Named
login/password-reset policies and distributed token/verification limits remain
active. There is no IP allowlist, caller-controlled quota key, disabled limiter,
extended authorization cache lifetime, or fail-open authorization.

## Verification and operational limit

`IdentityValidationRateLimitTests` proves 120 session checks no longer block a
signing-key query or general request; each class still rejects at its own limit,
uses separate remote-IP budgets, ignores spoofed caller headers, and does not
create partitions from arbitrary paths. Session-validation tests retain denial
of absent, invalid, and revoked actors.

Authorization failure at subscribe time remains fail-closed. Bolt does not report
a subscription acknowledgement to the caller; healthy transport alone therefore
does not prove subscription authorization. After deploying normally, verify fresh
multi-user subscriptions and renewals, and check Hub logs for both unauthorized
subscriptions and IdentityServer 429s. Do not work around failures with admin
capabilities or by extending stale-session acceptance.
