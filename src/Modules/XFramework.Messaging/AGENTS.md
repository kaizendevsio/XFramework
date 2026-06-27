# Messaging Module Agent Guide

Use this file for Messaging-specific context after reading the repository root `AGENTS.md`, `CLAUDE.md`, `rules/BackendGuidelines.md`, and `rules/UiGuidelines.md` when UI is involved.

## Module Purpose

Messaging is the tenant chat platform module. It owns chat semantics: threads, direct threads, messages, members, read state, reactions, attachments, pins, saves, blocks, reports, moderation rules, templates, settings, admin read projections, realtime chat events, and Messaging outbox records.

Messaging is not the owner of external delivery infrastructure. Email, SMS, webhook, and future push delivery are delegated to the Notifications module. SmsGateway owns SMS node job processing, but Messaging must reach it only through Notifications.

## Current Architecture

- API code lives in `Messaging.Api` and follows the feature/VSA and service patterns used in this repo.
- Shared entities, EF configurations, request/response contracts, constants, settings catalog, and realtime types live in `Messaging.Domain.Shared`.
- Client-facing wrappers and .NET chat helpers live in `Messaging.Integration`.
- Tests live in `Messaging.Tests`; add or update them when Messaging behavior changes.
- Realtime committed Messaging events use Bolt + MemoryPack. This product stack is .NET/C#/Blazor-first; do not add TypeScript Messaging SDKs or parallel JSON chat topics unless a new architecture decision explicitly requires it.

## Integration Rules

- Use `IMessagingServiceWrapper` or `IMessagingChatClient` for tenant/chat app integration.
- ControlPanel must use Messaging wrapper-backed admin/settings/template APIs for Messaging business behavior. Do not use direct `IDataContext` mutation from ControlPanel for Messaging settings, moderation, templates, messages, threads, or delivery actions.
- Messaging API services must resolve trusted tenant/credential context through `IMessagingRequestContextResolver`; do not trust client-supplied tenant IDs on protected paths.
- Chat APIs must enforce Messaging feature gates, membership checks, block policy, tenant isolation, policy settings, and rate limits in service logic.
- External direct-message transports must be queued through Notifications. Do not inject or call `ISmsGatewayServiceWrapper` from Messaging business services.
- Notifications owns provider dispatch and delivery state for SMS/email/webhook/push. Messaging may store audit/correlation fields but must not duplicate provider send logic.
- Binary upload/download remains owned by Storage. Messaging only validates and links `StorageFile` records for attachments.
- Redis is used by Bolt for production durable realtime queues. Do not use Redis as Messaging source-of-truth data storage.

## Realtime Rules

- Publish committed chat events through the Messaging outbox and realtime publisher; do not publish durable user/thread events directly from endpoint handlers before the database commit is complete.
- Durable topics must be tenant/user/thread scoped and authorized. Avoid public thread-only topics.
- Typing and presence are transient Bolt events. Do not persist every typing event.
- Acknowledge durable events only after handlers succeed in client/helper code.

## Settings And Templates

- Use `MessagingSettingsCatalog` as the authoritative list of supported settings.
- Settings reads/writes must go through `MessagingSettingsService` and wrapper contracts. Do not mutate `RegistryConfiguration` directly from UI pages.
- Message templates are real `MessageTemplate` records. System templates are seeded/read-only; tenant templates are admin-configurable; user templates are app/user-owned and only audited in ControlPanel unless a later plan expands that scope.
- Template rendering happens server-side in Messaging. Preserve token validation and template audit fields on messages/direct messages.

## Data And EF Rules

- Keep Messaging tables in the `Messaging` schema.
- Add EF configurations in `Messaging.Domain.Shared`; include indexes for tenant-scoped query shapes.
- Use projections and pagination for admin/read APIs. Do not add unbounded list queries.
- Prefer explicit service methods and request contracts over generic CRUD for business behavior.
- Migrations are generated through the shared migration runner path. Do not run migrations at service startup.

## Testing Expectations

- Run `Messaging.Tests` after changes to Messaging behavior.
- Add focused tests for tenant isolation, feature gates, membership/permission rules, block filtering, policy/rate-limit enforcement, outbox behavior, templates, attachments, moderation, and wrapper/admin-read behavior.
- If changes touch external delivery integration, also run or update Notifications/SmsGateway tests.
- If changes touch realtime transport or topic authorization, also run or update Bolt tests.
- If changes touch ControlPanel, build `ControlPanel.Server` and browser-smoke the affected pages.

## Do

- Keep Messaging as the source of truth for chat behavior.
- Keep Notifications as the source of truth for external delivery jobs, provider attempts, retries, and status.
- Keep admin UI privacy-safe by showing metadata and short previews unless moderation context explicitly requires more.
- Keep routes and wrapper contracts backward-compatible unless a migration/breaking-change plan exists.
- Update this `AGENTS.md` when Messaging feature behavior, integration boundaries, realtime contracts, delivery delegation, settings/templates, or testing requirements change.

## Do Not

- Do not bypass wrappers or service methods for Messaging business workflows.
- Do not add direct cross-module writes from Messaging into Notifications, SmsGateway, Storage, or Identity schemas.
- Do not reintroduce direct SmsGateway sends from Messaging.
- Do not add TypeScript or JSON duplicate realtime paths for Messaging chat without an explicit .NET stack architecture change.
- Do not expose full message content in ControlPanel lists by default.
- Do not add in-memory queues for production Messaging, Notifications, or SmsGateway delivery workflows.
- Do not leave this guide stale after bug fixes, feature changes, or integration contract changes in this module.
