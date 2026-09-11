---
title: "Actor-isolated transient subscriptions on shared Bolt clients"
date: 2026-09-11
category: architecture-patterns
module: Bolt
problem_type: integration_issue
component: pubsub
severity: high
tags: [bolt, subscriptions, authorization, blazor]
---

# Actor-isolated transient subscriptions

A server-side Blazor host shares one Bolt transport across users. A single channel
per topic prevented the second user from subscribing to typing/presence. Simply
fanning out the first user's events locally would bypass the second user's topic
authorization.

Each transient enumeration now receives a random `clientId~guid` subscriber ID.
The Hub validates each Subscribe frame with that enumeration's actor token and
retains a separate connection-owned binding. Events carry the existing subscriber-ID
field and the client delivers only to that exact channel. Untagged events are never
fanned out to these actor channels. Scoped subscriptions stop receiving at actor
token expiry; reconnect resolves and authorizes each actor's refreshed token.

Cancellation removes only that enumeration. A per-subscription binding gate orders
reconnect against cancellation so an already removed enumeration cannot be rebound.
Explicit topic unsubscribe only matches subscriptions with the supplied actor token
(or actorless subscriptions for the actorless overload).

Legacy clients using the registered client ID retain their one untagged subscription
per topic/connection. New clients require the updated Hub; deploy the Hub before
rebuilding/restarting clients. Durable subscription IDs, acknowledgements, and replay
are unchanged. Each scoped transient binding counts toward the existing principal
subscription quota and is removed on transport disconnect.

Regression coverage lives in `BoltTransientActorIntegrationTests`: two and three
actors on one connection, independent cancellation, refreshed-token reconnect,
unauthorized and expired actors, and permission removal on reconnect. The actual
Communications authorizer tests also exercise scoped IDs against PostgreSQL tenant
and active-membership checks, plus malformed and foreign-client IDs.
