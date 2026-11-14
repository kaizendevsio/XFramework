# Portal Login Configuration

The login card uses `XSystems` when no brand is configured. An optional logo replaces
the wordmark; a failed image falls back to the configured display name or `XSystems`.
No public tenant lookup or tenant selector is needed on the login page.

With no `Portal:Login` configuration, sign-in retains the platform admin tenant and
role. This is the default for Xeon Dev; `Tenant:DefaultId` is not a login setting.

## Single-Tenant Deployment

Configure `Portal:Login:Default` with `TenantId`, `RoleTypeId`, `DisplayName`, and
optional `LogoUrl`. Both IDs must reference an existing tenant and an existing role
type assigned to the users who may sign in. This does not create roles or grant
permissions. IdentityServer remains responsible for credential and role validation.

## Host-Specific Deployment

Configure an array at `Portal:Login:Hosts`. Each entry has `Host` (hostname only,
without scheme, port, or path) and the same fields as `Default`. For example:

```json
{
  "Portal": {
    "Login": {
      "Hosts": [
        { "Host": "admin.example.com" },
        {
          "Host": "shop.example.com",
          "TenantId": "11111111-1111-1111-1111-111111111111",
          "RoleTypeId": "22222222-2222-2222-2222-222222222222",
          "DisplayName": "Example Shop",
          "LogoUrl": "/branding/shop.png"
        }
      ]
    }
  }
}
```

Replace example IDs with real assigned IDs. An entry without either ID explicitly
selects platform admin login. When `Hosts` is configured, unknown hosts cannot sign
in and do not fall back to `Default`. Cookies are checked against the site's tenant
and role. Tenant users cannot select another tenant from the Portal profile menu.

Serve logos from a public local absolute path or a trusted HTTPS URL. Configure DNS,
TLS, reverse-proxy host preservation and ASP.NET `AllowedHosts` for the deployment.
Do not trust arbitrary forwarded-host headers or expose the upstream app publicly.
Configuration changes require a restart. This adds configuration-backed branding,
not a tenant-logo upload or domain-management UI.

## Verification (2026-11-14)

- Portal/test project build passed.
- 125 targeted authentication, cookie/session, tenant-binding and layout contract
  tests passed. One existing Redis persistence test skipped because its dedicated
  test connection was not configured.
- Eight browser checks passed: desktop/mobile (including 320px), light/dark,
  required fields/error feedback, configured logo, and broken-logo fallback.
- Tenant authentication routing and response mismatch rejection were tested with
  mocked IdentityServer responses. A real tenant user login on a configured domain
  has not been exercised; configure actual tenant/role IDs before that acceptance test.
- Hostname fixtures were local only. Xeon Dev's configuration and deployment were
  not changed. The final local preview retains the unconfigured platform login.
