---
title: "Intermittent Audit 401 after canceled credential-policy refresh"
date: 2026-09-10
category: developer-experience
module: XFramework.Integration
problem_type: runtime_bug
component: service_authorization
status: verified-by-regression-tests
---

# Intermittent Audit authorization failure

## Symptom and evidence

An authenticated Portal user could load the audit list but intermittently fail to open event 43790. Reloading was not reliable. The event existed and services were healthy. A fresh login reproduced the failure, excluding ordinary user-session expiry as the explanation for that reproduction.

On xeon-dev, token-free generated-handler diagnostics identified the rejection:

`Bolt authorization rejected GetAuditEventRequest: Status=401, Reason=Service token credential generation is not accepted.`

## Cause

`IdentityServerSigningKeyProvider.IsAcceptedAsync` set its refresh-attempt timestamp **before** fetching the service credential-generation policy. If navigation canceled that fetch, the timestamp remained but no fresh policy was available. A following request entered the cooldown branch and returned `false`, which became a 401 for a valid service credential. Failed HTTP fetches had the same effect. The default cooldown is five seconds; repeated navigation could trigger the problem again.

The audit detail page maps unsuccessful responses to a generic unavailable/access message. That message did not mean the audit record was missing or that the user's role had changed.

## Fix and security properties

Start the unknown-generation cooldown only after a successful policy fetch and cache publication. A canceled/failed fetch remains retryable. Keep the refresh semaphore, bounded policy TTL, and rejection of genuinely unknown/retired generations. Do not accept expired policy, bypass authorization, extend user sessions, or add blind UI retries.

Generated Bolt handlers now log rejection status, reason and request ID server-side, without logging tokens or request payloads.

## Regression coverage

`IdentityServerSigningKeyProviderTests` covers canceled refresh followed by a valid request, failed HTTP refresh followed by recovery, a waiting concurrent caller recovering after cancellation, and rejection/throttling of an unknown generation after a completed policy fetch. The first two tests failed before the fix and pass afterward. The existing Attendance CI authorization step includes this fixture.

The broader local Core security run also reported two unrelated existing source-guard failures in `IdentityAuthorizationService.cs` and `AdvancedWallets/Endpoint.cs`; those files are unchanged by this fix.
