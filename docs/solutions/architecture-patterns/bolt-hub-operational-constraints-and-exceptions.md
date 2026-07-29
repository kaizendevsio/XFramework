---
title: "Bolt Hub Operational Constraints and Exceptions"
date: 2026-07-07
category: architecture-patterns
module: Bolt
problem_type: architecture_decision
component: bolt_hub
severity: medium
applies_when:
  - "Deploying Bolt Hub service discovery or changing Bolt Hub topic authorization"
tags: [bolt, bolt-hub, service-discovery, authorization, architecture-exception]
---

# Bolt Hub Operational Constraints and Exceptions

## Service Discovery Presence

Bolt Hub service discovery presence is currently single-instance only.

`BoltServiceDiscoveryRegistry` persists service manifest records, but live connection membership is tracked in the in-memory `IBoltServicePresenceTracker`. On startup, `ResetPresenceAsync` clears the local tracker and marks persisted records disconnected because the new process cannot know which old connections survived. This is correct for a single Hub instance, but unsafe for horizontal Hub deployments: a second instance or rolling restart can mark services connected to another instance offline.

Until instance-scoped leases or heartbeats are implemented, deploy exactly one Bolt Hub instance for service discovery presence.

Required future work before horizontal scaling:
- Persist Hub instance IDs with per-instance service leases.
- Refresh leases from active connections.
- Calculate service connection count from non-expired leases instead of local memory.
- Reset only the current instance lease set on startup/shutdown.

## Topic Authorization Cross-Module Read Exception

`CommunicationsBoltTopicAuthorizer` is an approved narrow exception to the usual "owning module contract/read model" boundary. It reads Identity credential state and Communications thread membership through the shared single database to authorize security-sensitive Bolt topic access synchronously in the Hub receive path.

This exception is allowed only for Communications topic authorization and only for read-only checks. The authorizer must not mutate Identity or Communications data, must filter by tenant and enabled/deleted flags, and must keep the topic contract in sync with the Communications module.

## Required Production Wiring

XFramework Bolt Hub sets `BoltConfiguration:RequireTopicAuthorization` to `true`. Resolving `BoltServer` fails at startup when that option is enabled and no `IBoltTopicAuthorizer` is registered. The reusable Bolt server default remains `false` so embedding `MapBolt()` does not silently impose an XFramework domain dependency.

`CommunicationsBoltTopicAuthorizer` is the current `IBoltTopicAuthorizer` implementation. Bolt invokes it for Subscribe, Publish, Unsubscribe, and Ack. Unknown or malformed topic namespaces are denied. Do not register independent boolean authorizers for unrelated namespaces: the current contract combines all registered authorizers as required checks. Introduce a namespace dispatcher only when a second production topic domain exists.

Preferred future direction:
- Move the checks behind owning-module authorization contracts or dedicated read models.
- Keep the Hub as a routing surface once those contracts are available.
