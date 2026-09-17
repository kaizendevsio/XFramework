---
title: "Yap client EF Core compiled model"
date: 2026-09-17
category: developer-experience
module: XFramework.Yap
problem_type: performance
component: development_workflow
severity: medium
status: current
applies_when:
  - "Changing OfflineDatabase entities, keys, or indexes in the Yap Blazor WebAssembly client"
tags: [yap, blazor, webassembly, efcore, sqlite, startup, performance]
---

# Yap client EF Core compiled model

`OfflineDatabase` runs EF Core over SQLite inside the browser. EF normally builds its model
by reflecting over the entity classes on every launch, and on WebAssembly that reflection is
paid at each cold start. `dotnet ef dbcontext optimize` replaces it with generated code.

The generated model lives in
`src/Presentation/XFramework.Yap.Client/Services/Generated/` and is applied once, in
`Program.cs`, through `UseModel(OfflineDatabaseModel.Instance)`.

## Why it is generated from the test project

`UseSqliteWasm` configures the ordinary `Microsoft.EntityFrameworkCore.Sqlite` provider with a
browser-backed connection, so a model generated against `UseSqlite` is exactly the model the app
uses. The client project itself cannot host the tool: `Microsoft.EntityFrameworkCore.Design`
would be published into the browser payload, and the Blazor WebAssembly host cannot be started
at design time.

`src/Tests/Yap.Client.Tests` already compiles the same `Services/*.cs` sources against real
SQLite, so it hosts `OfflineDatabaseDesignTimeFactory` and runs the tool. The generated files are
written straight into the client project and linked back into the test project, so both
assemblies compile the identical generated code.

## Regenerating

Run this after **any** change to the `OfflineDatabase` entities, keys, indexes, or
`OnModelCreating`:

```bash
dotnet ef dbcontext optimize \
  --project src/Tests/Yap.Client.Tests/Yap.Client.Tests.csproj \
  --context OfflineDatabase \
  --output-dir ../../Presentation/XFramework.Yap.Client/Services/Generated \
  --namespace Yap.Client.Services
```

`--nativeaot` and `--precompile-queries` are deliberately not used: the client is Mono AOT, not
NativeAOT, and both options are still experimental.

Then run the client tests. `CompiledModelTests` compares the generated model against a model
reflected from the entity classes, column by column, and fails when they disagree — a stale
compiled model would otherwise read and write the wrong columns without any build error.

## The threading switch in `Program.cs`

EF generates a static constructor that runs the model initializer on a fresh 10 MB-stack thread.
WebAssembly is single-threaded here, so `Thread.Start` throws and startup dies inside
`OfflineDatabaseModel` with `PlatformNotSupportedException` — a failure no build or host-side test
reproduces. `AppContext.SetSwitch("Microsoft.EntityFrameworkCore.Issue31751", true)` runs that
initializer on the calling thread instead and must stay the first statement in `Program.cs`.
Keep it if a regenerated model still emits the thread; drop it only after checking
`Services/Generated/OfflineDatabaseModel.cs`.

## Relationship to the schema upgrade path

The compiled model changes only how EF *describes* the schema, never how the database is
migrated. `DatabaseStartup` still calls `EnsureCreatedAsync` followed by
`OfflineDatabase.UpgradeAsync`, whose `pragma_table_info`-guarded `ALTER TABLE` statements stay
idempotent: on a database the compiled model just created, every column already exists and each
statement is skipped. `CompiledModelTests` runs that exact sequence twice to keep it honest.
