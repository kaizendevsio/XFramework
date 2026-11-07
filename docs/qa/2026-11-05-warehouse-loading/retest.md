# Warehouse Loading Fix and Live Retest

Date: 2026-11-05. Target: Xeon Dev, Jay Test 01.

## Defect and Fix

The warehouse page queried optional variations and traceability lots unconditionally before loading locations, stock balances and movements. Jay Test 01 has Traceability disabled. A rejected lookup left the page displaying empty collections as zero counts despite existing stock.

- Guard optional lookups with their respective tenant feature flags.
- Reset optional lookup lists on reload.
- Render a persistent error and Retry action when loading fails, instead of rendering partial data as an empty warehouse.
- No tenant configuration, warehouse, location, balance or movement records were changed.

## Automated Verification

39 targeted tests passed, including five new WarehouseDetailLoadingTests cases:

- All four enabled/disabled combinations of Variations and Traceability load locations, balances and movements without querying disabled features.
- A failed location read hides partial counts, does not disclose backend exception text, and successfully retries.
- Existing Inventario navigation, grid filtering, control rendering and Portal architecture checks pass.

Portal compiled as part of the test build. The Inventario feature RCL Release build passed with zero errors. Existing package/compiler warnings remain, including the MessagePack NU1902 advisory.

```powershell
dotnet test src/Tests/Portal.E2ETests/Portal.E2ETests.csproj -m:1 /nr:false --filter FullyQualifiedName~WarehouseDetailLoadingTests --logger 'trx;LogFileName=warehouse-loading.trx'
dotnet test src/Tests/Portal.E2ETests/Portal.E2ETests.csproj --no-build --filter 'FullyQualifiedName~InventarioNavigationTests|FullyQualifiedName~InventarioGridFilteringTests|FullyQualifiedName~InventarioControlRenderingTests|FullyQualifiedName~WarehouseDetailLoadingTests|FullyQualifiedName~PortalArchitectureTests' --logger 'trx;LogFileName=warehouse-regressions.trx'
dotnet build src/Presentation/XFramework.Portal.Features.Inventario/XFramework.Portal.Features.Inventario.csproj -c Release --no-restore -m:1 /nr:false
```

## Deployment

Docker context: `xeon-dev`. SSH: `xeon@xeon-dev`.

Portal-only hotfix image derived from the existing `xframework-portal:qa-fixes-20261029` image. Only the rebuilt Inventario feature DLL and PDB were replaced; the existing Portal host, dependencies, assets and backend services were preserved. No database migrations, resets or volume changes.

- Image: `xframework-portal:warehouse-fix-20261105`
- Image ID: `sha256:02617a1d7fe0dca774066b0d48f9bdb98a4b8ef4f1946d7b9bc172347a3ba52c`
- Container started: `2026-11-05T14:15:20Z`; health: healthy.
- Inventario UI DLL SHA256, verified inside the container: `cb216b091a46fd67f0d564a907089449ac75298916b5ee11e1bedbff199e68b3`
- Source remains uncommitted in the current worktree; no push or merge was requested in this turn.

Remote deployment command:

```sh
sudo -n docker compose --project-directory /home/github-runner/xframework-deploy/releases/39825885286-1 --env-file /home/github-runner/xframework-deploy/releases/39825885286-1/candidate.env -p xframework -f /home/github-runner/xframework-deploy/releases/39825885286-1/docker-compose.yml -f /home/github-runner/xframework-deploy/releases/39825885286-1/images.override.json -f /home/xeon/qa-fixes-20261029.override.json -f /home/xeon/warehouse-fix-20261105.override.json up -d --no-deps --no-build --pull never --wait --wait-timeout 180 portal
```

Rollback: repeat this command without the final `warehouse-fix-20261105.override.json` override. This restores the previous Portal image only.

## Live Browser Retest

URL: <http://xeon-dev:5000/inventario/warehouses/41d5c0cc-462b-45da-8e82-4e65ba3b7c14>

Tenant: Jay Test 01 (`69fa3896-0afe-42fa-a11f-5e265207d9d6`). Basic setup and disabled Traceability were left unchanged.

| Check | Result |
| --- | --- |
| Summary counts | PASS: 1 location, 1 stock balance, 1 recent movement |
| Locations tab | PASS: STOCK / General stock / Bin / Pickable |
| Balances tab | PASS: T-shirt base product, General stock, 10 on hand, 0 reserved, 10 available |
| Movements tab | PASS: T-shirt Receipt, delta +10, on hand after 10, displayed date 11/05/2026 13:35:15 |
| Refresh and fresh navigation | PASS: counts and location remain correct |
| Product stock cross-check | PASS: same warehouse/location and quantities |
| Portal logs since restart | No matches for fail:, error or exception during verification |

Evidence: [locations](screenshots/locations.png), [balances](screenshots/balances.png), [movements](screenshots/movements.png). The movements screenshot was visually inspected. Tests used the existing business records read-only; no stock posting or feature enablement was needed.
