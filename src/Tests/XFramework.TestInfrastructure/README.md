# XFramework Integration Test Standard

Use this project as the shared kit for module-level API integration tests.

## Project Shape

Each module keeps its own integration test project:

- `IdentityServer.IntegrationTests`
- `Wallets.IntegrationTests`
- `Inventario.IntegrationTests`

Use this structure inside each project:

- `Infrastructure/<Module>IntegrationTestFixture.cs`
- `Infrastructure/<Module>TestBase.cs`
- `Infrastructure/<Module>TestAuthHandler.cs`
- `Tests/<Area>Tests.cs`

## Runtime Pattern

Integration suites should run a disposable PostgreSQL Testcontainer, migrate `AppDbContext`, seed shared identity/tenant/module data, start Bolt Hub, start the module API, then start a small test client app with the generated service wrapper and `AddRemoteDataContext()`.

Tests should exercise behavior through HTTP, generated service wrappers, and remote `IDataContext`. Direct `AppDbContext` access is for seeding and final assertions only.

## Categories

Use constants from `TestCategories`:

- `Kind:Integration`
- `Module:IdentityServer`, `Module:Wallets`, `Module:Inventario`
- `Area:Auth`, `Area:DataContext`, `Area:FeatureGates`, `Area:Catalog`, `Area:Warehousing`, `Area:Traceability`, `Area:Stock`, `Area:Reservations`, `Area:Planning`, `Area:Reporting`, `Area:Purchasing`

Example filter:

```powershell
dotnet test src/Tests/Inventario.IntegrationTests/Inventario.IntegrationTests.csproj --filter "TestCategory=Area:Stock"
```

## Test Auth Headers

Use constants from `TestAuthHeaders`:

- `X-Test-TenantId`
- `X-Test-IdentityId`
- `X-Test-CredentialId`
- `X-Test-Username`
- `X-Test-Roles`
- `X-Test-Unauthenticated`

Default test auth should authenticate as a seeded admin user unless a test explicitly overrides headers.
