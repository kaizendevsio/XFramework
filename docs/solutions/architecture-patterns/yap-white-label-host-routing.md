---
title: "Yap white-label host routing"
date: 2026-10-08
category: architecture-patterns
module: Yap
problem_type: feature
component: tenant_routing
tags: [yap, tenants, branding, pwa, security]
---

# Yap white-label host routing

One Yap host can serve separately branded tenant origins with the same WebAssembly
bundle. Add exact host mappings under `Yap:Hosts`; tenant and regular-member role IDs
come from the operator's configuration, never from browser request fields.

```json
{
  "AllowedHosts": "chat.alpha.example;chat.beta.example",
  "Yap": {
    "Hosts": {
      "chat.alpha.example": {
        "TenantId": "11111111-1111-1111-1111-111111111111",
        "RoleId": "22222222-2222-2222-2222-222222222222",
        "Branding": {
          "Name": "Alpha Chat",
          "ShortName": "Alpha",
          "Tagline": "Keep the conversation going.",
          "LogoUrl": "/brands/alpha.svg",
          "AccentColor": "#336699",
          "Icon192Url": "/brands/alpha-192.png",
          "Icon512Url": "/brands/alpha-512.png",
          "AppleIconUrl": "/brands/alpha-apple.png"
        }
      },
      "chat.beta.example": {
        "TenantId": "33333333-3333-3333-3333-333333333333",
        "RoleId": "44444444-4444-4444-4444-444444444444",
        "Branding": { "Name": "Beta Chat", "ShortName": "Beta", "AccentColor": "#cceeff" }
      }
    }
  }
}
```

Each mapped tenant and role must already be provisioned in IdentityServer and the
backend modules. Registration still uses IdentityServer's permitted regular-member
role, and Yap checks that the response matches the configured role. Configure the
existing Yap service identity with the required module audiences and scopes; actor
requests retain their user's tenant and access token. This does not provision
tenants, roles, databases, or cross-module permissions.

Configure DNS and TLS for every origin. The ingress must preserve the validated
original `Host` header and accept only the configured public hosts. Yap selects
`Request.Host.Host`, ignoring `X-Forwarded-Host` and other client tenant selectors.
It does not install forwarded-header middleware. If another trusted hosting layer
rewrites `Host`, secure that layer's proxy allowlist and ingress policy first.
`AllowedHosts` should list the same origins; the Yap mapping provides an additional
exact allowlist. Hostnames cannot contain wildcard patterns, schemes, ports, or a
trailing dot. Ports do not distinguish tenants. Unknown mapped hosts return `421`
before sign-in, protected endpoints, or static assets. `/health/*` remains available
to container probes independently of public host routing.

An authenticated cookie with a different tenant than its requested host returns
`403` before session touch, OPAQUE actor lookup, HTTP business work, or WebSocket
upgrade. The original tenant's session remains valid. Session claims must match the
encrypted session entry's tenant and credential before actor work or revocation.
Existing chat tickets, call tickets, keys, membership checks, and tenant-aware cache
keys retain their existing binding. Cookies have no shared domain; browser storage,
offline chat caches, accent preferences and worker caches are isolated by origin.
Do not remap an existing public origin to a different tenant without retiring its
old offline installation and browser storage.

`/api/branding` returns only the public presentation fields. Names and taglines are
bounded printable text, colors are six-digit hex values, and logos/icons are
same-origin absolute image paths without query strings or traversal. Place those
assets in the published web root; use actual 192px/512px PNG icons and a 180px Apple
icon, and confirm their responses and dimensions before deployment. Configured
assets are public. Defaults retain Yap's assets when a field is omitted. The
mapping is validated at startup and changes require a restart.

The browser uses cached public branding or its default for the first interactive render. The network refresh updates the branding afterward without blocking startup. Brand
components, sign-in copy, page titles, install guidance, app metadata and the accent
default use that configuration. A user's selected accent remains their preference.
Branding is stored in local storage on that origin for offline startup. If the
public request fails, the app keeps the last branding or its Yap defaults. Product
release notes and recovery/diagnostic documents retain Yap's product identity.

Mapped installations link to `/api/branding/manifest`, which provides that tenant's
name, icons and theme color with `no-store`. The worker already passes `/api/*`
through to the network, so tenant-dependent manifest bytes are excluded from the
static shell's build-hash verification. The same application and worker bundle is
published for all origins. Existing PWA installations may require the browser's
normal manifest refresh or reinstallation to pick up a changed app name/icon.

When `Yap:Hosts` is absent, the existing `Yap:TenantId` and `Yap:RoleId` configuration
continues to select one tenant on every permitted host. The static
`/manifest.webmanifest` and the default Yap branding remain compatible.

Redis session persistence and Data Protection keys retain their existing
configuration. Calls, socket admissions and active call state remain in memory on
one Yap instance; host mapping does not provide automatic horizontal sharding.
Keep participants on the same instance and retain the existing service scaling
limitations. A mapped host's tenant isolation is not a separate process or database.

Focused verification lives in `TenantRoutingEndpointTests`, `YapSessionsTests`, and
`branding.test.mjs`: two hosts, password/OPAQUE binding, copied-cookie crossover,
unknown hosts, forwarded-host spoofing, invalid configuration, manifests, legacy
mode and public/offline branding behavior.
