---
title: "Portal Feature Razor Class Library Architecture"
date: 2026-09-02
category: developer-experience
module: XFramework.Portal
problem_type: architecture
component: portal_composition
severity: high
applies_when:
  - "Adding or moving Portal pages and presentation services"
  - "Changing Portal routing, dependency injection, or feature project references"
tags: [portal, blazor, razor-class-library, modular-monolith, architecture]
status: current
---

# Portal Feature Razor Class Library Architecture

Portal is one Blazor Server application and one deployable service. Its UI is organized as a modular monolith: the host owns runtime composition and feature Razor Class Libraries own feature pages and presentation services.

## Project Boundaries

- `XFramework.Portal` owns authentication, sessions, login, dashboard, layout, navigation, theme, active-tenant selection, module wrapper composition, health checks, and executable startup.
- `XFramework.Portal.Shared` owns reusable UI components and narrow host-provided contracts such as `IPortalTenantContext`, `IPortalModuleAvailability`, and `IPortalActorContext`.
- `XFramework.Portal.Features.*` projects own feature routes, feature UI, and feature-specific presentation services.
- A feature RCL may reference `XFramework.Portal.Shared` and the backend contracts or wrappers it uses.
- A feature RCL must not reference the Portal host or another feature RCL.
- Cross-feature composition pages remain in the host unless their shared behavior is extracted behind a host-independent contract. `UserDetail.razor` is one such composition because it presents Identity and Attendance data together.

## Feature Catalog

The active feature projects are:

- `XFramework.Portal.Features.Identity`
- `XFramework.Portal.Features.Administration`
- `XFramework.Portal.Features.Inventario`
- `XFramework.Portal.Features.POS`
- `XFramework.Portal.Features.Finance`
- `XFramework.Portal.Features.Attendance`
- `XFramework.Portal.Features.Community`
- `XFramework.Portal.Features.Communications`
- `XFramework.Portal.Features.Storage`

## Routing And Startup

Every feature exposes a marker type. Add its assembly once to `PortalFeatureAssemblies.All`; the host supplies that same catalog to both the Blazor Router `AdditionalAssemblies` parameter and interactive endpoint `AddAdditionalAssemblies` call. Do not add a second discovery mechanism.

Feature-specific services are registered through an `Add<Feature>PortalFeature` extension when registration is needed. Wrapper infrastructure and remote `IDataContext` composition remain centralized in the host.

## Verification

Run the Portal build and the non-browser Portal contract suite. `PortalArchitectureTests` enforces project-reference direction, the expected feature catalog, and unique routes across host and feature assemblies. Browser-smoke at least one host route and representative routes from changed feature assemblies.

```powershell
dotnet build src/Presentation/XFramework.Portal/XFramework.Portal.csproj -m:1 /nr:false
dotnet test src/Tests/Portal.E2ETests/Portal.E2ETests.csproj -m:1 /nr:false --filter "FullyQualifiedName!~Portal.E2ETests.PortalE2ETests."
```
