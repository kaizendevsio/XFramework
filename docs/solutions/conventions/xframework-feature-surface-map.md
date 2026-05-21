---
title: "XFramework Feature Surface Map"
date: 2026-05-21
category: conventions
module: XFramework
problem_type: convention
component: feature_inventory
severity: high
applies_when:
  - "Finding the module, project, or representative path for a feature change"
  - "Reconciling module project inventory before planning or implementation"
tags: [features, modules, inventory, vsa, agent-onboarding]
status: current
---

# XFramework Feature Surface Map

Use this as a current orientation index for modules and feature surfaces. It does not replace detailed implementation rules in [best practices](xframework-best-practices.md), the [VSA agent playbook](xframework-vsa-agent-playbook.md), or subsystem docs.

## Mandatory Module Project Inventory

This inventory reconciles every `src/Modules/**/*.csproj` into one required category.

| Category | Projects |
|---|---|
| API | `src/Modules/XFramework.IdentityServer/IdentityServer.Api/IdentityServer.Api.csproj`, `src/Modules/XFramework.Wallets/Wallets.Api/Wallets.Api.csproj`, `src/Modules/XFramework.Messaging/Messaging.Api/Messaging.Api.csproj`, `src/Modules/XFramework.Community/Community.Api/Community.Api.csproj`, `src/Modules/XFramework.SmsGateway/SmsGateway.Api/SmsGateway.Api.csproj`, `src/Modules/XFramework.Inventario/Inventario.Api/Inventario.Api.csproj`, `src/Modules/XFramework.Coins/Server/Coins.Api/Coins.Api.csproj`, `src/Modules/XFramework.Bolt/Bolt.Hub/Bolt.Hub.csproj` |
| Domain.Shared | `src/Modules/XFramework.IdentityServer/IdentityServer.Domain.Shared/IdentityServer.Domain.Shared.csproj`, `src/Modules/XFramework.Wallets/Wallets.Domain.Shared/Wallets.Domain.Shared.csproj`, `src/Modules/XFramework.Messaging/Messaging.Domain.Shared/Messaging.Domain.Shared.csproj`, `src/Modules/XFramework.Community/Community.Domain.Shared/Community.Domain.Shared.csproj`, `src/Modules/XFramework.SmsGateway/SmsGateway.Domain.Shared/SmsGateway.Domain.Shared.csproj`, `src/Modules/XFramework.Inventario/Inventario.Domain.Shared/Inventario.Domain.Shared.csproj`, `src/Modules/XFramework.Payments/Payments.Domain.Shared/Payments.Domain.Shared.csproj`, `src/Modules/XFramework.Bolt/Bolt.Domain.Shared/Bolt.Domain.Shared.csproj` |
| Integration | `src/Modules/XFramework.IdentityServer/IdentityServer.Integration/IdentityServer.Integration.csproj`, `src/Modules/XFramework.Wallets/Wallets.Integration/Wallets.Integration.csproj`, `src/Modules/XFramework.Messaging/Messaging.Integration/Messaging.Integration.csproj`, `src/Modules/XFramework.SmsGateway/SmsGateway.Integration/SmsGateway.Integration.csproj` |
| UI/client | `src/Modules/XFramework.Blazor/XFramework.Blazor.csproj`, `src/Modules/XFramework.Coins/Client/Coins.Web/Coins.Web/Coins.Web.csproj` |
| Tests | `src/Modules/XFramework.Messaging/Messaging.Tests/Messaging.Tests.csproj`, `src/Modules/XFramework.Coins/Server/Coins.Tests/Coins.Tests.csproj` |
| Library/core | `src/Modules/XFramework.Payments/Payments.Core/Payments.Core.csproj` |
| Historical/deferred | None under `src/Modules/**/*.csproj`. The historical/deferred StreamFlow reference is in `.workflows/streamflow.service.yaml` and points to missing `src/Modules/XFramework.StreamFlow/StreamFlow.Stream/StreamFlow.Stream.csproj`. |

## Module Feature Surfaces

| Module | Main surface | Representative paths | Notes |
|---|---|---|---|
| IdentityServer | Authentication, verification, credentials, files, health, identity contracts, integration wrappers | `src/Modules/XFramework.IdentityServer/IdentityServer.Api/Features/Auth/Authenticate/Endpoint.cs`, `src/Modules/XFramework.IdentityServer/IdentityServer.Api/Features/Verification/Create/Endpoint.cs`, `src/Modules/XFramework.IdentityServer/IdentityServer.Domain.Shared/IdentityServer.Domain.Shared.csproj`, `src/Modules/XFramework.IdentityServer/IdentityServer.Integration/IdentityServer.Integration.csproj` | Also covered by integration tests under `src/Tests/IdentityServer.IntegrationTests/IdentityServer.IntegrationTests.csproj`. |
| Wallets | Wallet lifecycle, transfers, funds, withdrawals, events, batch operations, contracts, integration wrappers | `src/Modules/XFramework.Wallets/Wallets.Api/Features/Wallets/Transfer/Endpoint.cs`, `src/Modules/XFramework.Wallets/Wallets.Api/Features/Wallets/Create/Endpoint.cs`, `src/Modules/XFramework.Wallets/Wallets.Api/Features/Batch/TransferBatch/Endpoint.cs`, `src/Modules/XFramework.Wallets/Wallets.Domain.Shared/Wallets.Domain.Shared.csproj`, `src/Modules/XFramework.Wallets/Wallets.Integration/Wallets.Integration.csproj` | Also surfaced in ControlPanel finance pages and Wallets integration tests. |
| Messaging | Threads, messages, direct messages, reactions, attachments, contracts, integration wrappers, module tests | `src/Modules/XFramework.Messaging/Messaging.Api/Features/Messages/CreateMessage/Endpoint.cs`, `src/Modules/XFramework.Messaging/Messaging.Api/Features/Threads/Create/Endpoint.cs`, `src/Modules/XFramework.Messaging/Messaging.Domain.Shared/Messaging.Domain.Shared.csproj`, `src/Modules/XFramework.Messaging/Messaging.Integration/Messaging.Integration.csproj`, `src/Modules/XFramework.Messaging/Messaging.Tests/Messaging.Tests.csproj` | Use VSA docs for endpoint shape; this map only locates representative surfaces. |
| Community | Community identities, content, content files, reactions, feed, notifications, connections, contracts | `src/Modules/XFramework.Community/Community.Api/Features/Content/Create/Endpoint.cs`, `src/Modules/XFramework.Community/Community.Api/Features/CommunityIdentities/Create/Endpoint.cs`, `src/Modules/XFramework.Community/Community.Api/Features/Feed/GetFeed/Endpoint.cs`, `src/Modules/XFramework.Community/Community.Domain.Shared/Community.Domain.Shared.csproj` | Production-readiness context exists in [Messaging and Community readiness](../workflow-issues/messaging-community-production-readiness.md). |
| SmsGateway | SMS create/receive/confirm/query endpoints, contracts, integration wrappers | `src/Modules/XFramework.SmsGateway/SmsGateway.Api/Features/Sms/Create/Endpoint.cs`, `src/Modules/XFramework.SmsGateway/SmsGateway.Api/Features/Sms/CreateReceived/Endpoint.cs`, `src/Modules/XFramework.SmsGateway/SmsGateway.Api/Features/Sms/ConfirmSent/Endpoint.cs`, `src/Modules/XFramework.SmsGateway/SmsGateway.Domain.Shared/SmsGateway.Domain.Shared.csproj`, `src/Modules/XFramework.SmsGateway/SmsGateway.Integration/SmsGateway.Integration.csproj` | API plus Domain.Shared and Integration surfaces are present. |
| Inventario | Product CRUD feature endpoints and product contracts | `src/Modules/XFramework.Inventario/Inventario.Api/Features/Products/Create/Endpoint.cs`, `src/Modules/XFramework.Inventario/Inventario.Api/Features/Products/Update/Endpoint.cs`, `src/Modules/XFramework.Inventario/Inventario.Api/Features/Products/GetList/Endpoint.cs`, `src/Modules/XFramework.Inventario/Inventario.Domain.Shared/Inventario.Domain.Shared.csproj` | Compact VSA module with representative product slices. |
| Payments | Payment gateway abstractions, cash-in/cash-out requests, provider service registration | `src/Modules/XFramework.Payments/Payments.Core/Payments.Core.csproj`, `src/Modules/XFramework.Payments/Payments.Core/README.md`, `src/Modules/XFramework.Payments/Payments.Core/Services/PaymentGatewayService.cs`, `src/Modules/XFramework.Payments/Payments.Domain.Shared/Contracts/Requests/Create/CreateCashInRequest.cs`, `src/Modules/XFramework.Payments/Payments.Domain.Shared/Contracts/Requests/Create/CreateCashoutRequest.cs` | Library/core and Domain.Shared only; no Payments API project is present. |
| Coins | Bitcoin/blockchain API endpoint, service wrappers, cache service, Blazor client, tests | `src/Modules/XFramework.Coins/Server/Coins.Api/Features/Blockchain/Send/Endpoint.cs`, `src/Modules/XFramework.Coins/Server/Coins.Api/Services/BlockchainService.cs`, `src/Modules/XFramework.Coins/Server/Coins.Tests/Coins.Tests.csproj`, `src/Modules/XFramework.Coins/Client/Coins.Web/Coins.Web/Pages/Index.razor`, `src/Modules/XFramework.Coins/Client/Coins.Web/Coins.Web/Coins.Web.csproj` | Server API and legacy/client UI are both active repository surfaces. |
| Blazor | Shared Blazor client/state/action infrastructure, session/identity/wallet/files/cart/cache features, IndexedDB | `src/Modules/XFramework.Blazor/XFramework.Blazor.csproj`, `src/Modules/XFramework.Blazor/Core/Features/BaseActionHandler.cs`, `src/Modules/XFramework.Blazor/Core/Features/Wallet/Behaviors/TransferWallet.cs`, `src/Modules/XFramework.Blazor/Core/Features/Session/Behaviors/Login.cs`, `src/Modules/XFramework.Blazor/Core/Services/IndexedDbService.cs` | Presentation apps consume these surfaces; UI rules live in UI/design docs. |
| Bolt Hub | Bolt hub service, queueing, telemetry/domain contracts, thin protocol, hosted service installers | `src/Modules/XFramework.Bolt/Bolt.Hub/Bolt.Hub.csproj`, `src/Modules/XFramework.Bolt/Bolt.Hub/Program.cs`, `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltHubService.cs`, `src/Modules/XFramework.Bolt/Bolt.Hub/ThinProtocol/BoltServer.cs`, `src/Modules/XFramework.Bolt/Bolt.Domain.Shared/Contracts/Requests/IBoltRequest.cs` | For protocol/library details, start with [Bolt unified transport layer](../architecture-patterns/bolt-unified-transport-layer.md) and `src/Libraries/Bolt/BOLT.md`. |
| Presentation | Control panel, gateway, Fluid app, generated/wrapper gateway endpoints, admin and finance/identity UI pages | `src/Presentation/ControlPanel.Server/ControlPanel.Server.csproj`, `src/Presentation/ControlPanel.Server/Components/Pages/Finance/Wallets.razor`, `src/Presentation/ControlPanel.Server/Components/Pages/Identity/Users.razor`, `src/Presentation/Gateway/Gateway.csproj`, `src/Presentation/Gateway/Endpoints/Endpoints.cs`, `src/Presentation/Fluid/Fluid.csproj` | `src/Presentation/BlueprintPoc/BlueprintPoc.csproj` exists but is not listed in `XFramework.slnx`; treat it as a local/poc surface unless separately brought into solution scope. |

## Cross-Cutting Surfaces Outside Modules

| Surface | Representative paths | Use when |
|---|---|---|
| Source generators | `src/SourceGenerators/XFramework.SourceGenerators/EntityEndpointGenerator.cs`, `src/SourceGenerators/XFramework.SourceGenerators/EntityServiceGenerator.cs`, `src/SourceGenerators/XFramework.SourceGenerators/ServiceWrapperGenerator.cs`, `src/SourceGenerators/XFramework.SourceGenerators/BoltHandlerGenerator.cs`, `src/SourceGenerators/XFramework.SourceGenerators/DataContextRegistrationGenerator.cs` | Changing generated endpoint, service, wrapper, Bolt handler, or data-context registration behavior. |
| Bolt libraries | `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs`, `src/Libraries/Bolt/Bolt.Client/BoltClient.cs`, `src/Libraries/Bolt/Bolt.Server/BoltServer.cs`, `src/Libraries/Bolt/Bolt.Media/BoltMediaClient.cs`, `src/Libraries/Bolt/Bolt.Media.Browser/BoltMediaService.cs` | Changing protocol, client/server transport, or media behavior. |
| Tests | `src/Tests/Bolt.Tests/TransportTests.cs`, `src/Tests/XFramework.Core.Tests/Services/Caching/HybridCacheServiceTests.cs`, `src/Tests/IdentityServer.IntegrationTests/Tests/AuthenticationTests.cs`, `src/Tests/Wallets.IntegrationTests/Tests/WalletTransactionTests.cs`, `src/Tests/ControlPanel.E2ETests/ControlPanelE2ETests.cs` | Finding representative coverage or deciding where new tests belong. |
| Tools | `src/Tools/XFramework.MigrationRunner/Program.cs` | Applying or validating migrations outside app startup. |
| Build/deploy/config | `XFramework.slnx`, `global.json`, `Directory.Packages.props`, `Directory.Build.props`, `Version.props`, `Dockerfile`, `.github/workflows/publish.yml`, `.workflows/streamflow.service.yaml` | Understanding solution membership, SDK/package versions, package publishing, container publishing, or historical/deferred workflow surfaces. |

## Historical Or Deferred Paths

| Path | Status |
|---|---|
| `src/Modules/XFramework.StreamFlow/StreamFlow.Stream/StreamFlow.Stream.csproj` | Historical/deferred. Referenced by `.workflows/streamflow.service.yaml`, but the path is not present in the current workspace. |
| `.workflows/streamflow.service.yaml` | Existing workflow config surface with stale StreamFlow/.NET 9 assumptions. Do not use it as current architecture guidance without a separate workflow modernization task. |

## How To Use This Map

Start with the module row that owns the behavior, open the representative source path, then follow the canonical docs for detailed rules. If a module has API, Domain.Shared, Integration, UI/client, and tests surfaces, keep changes scoped to the surface that owns the behavior instead of spreading implementation across layers by default.
