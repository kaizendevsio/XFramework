---
title: "Yap WebAssembly IL stripping is already on"
date: 2026-09-17
category: tooling-decisions
module: XFramework.Yap
problem_type: performance
component: build_pipeline
status: current
applies_when:
  - "Proposing WasmStripILAfterAOT or auditing the Yap client's published payload size"
tags: [yap, blazor, webassembly, aot, build, payload-size]
---

# Yap WebAssembly IL stripping is already on

`WasmStripILAfterAOT` is a recurring size proposal for the AOT-compiled Yap client. It is already
in effect and setting it in `XFramework.Yap.Client.csproj` would change nothing.

The .NET 10 WebAssembly SDK defaults it to `true`
(`Microsoft.NET.Runtime.WebAssembly.Sdk/10.0.9/Sdk/BrowserWasmApp.targets`), and the publish log
prints `IL stripping assemblies` on every Release build. It runs in the safe per-method mode
(`ILStrip TrimIndividualMethods="true"`): only method bodies the AOT compiler reported as fully
compiled are removed, so anything that still falls back to the interpreter keeps its IL. That is
why it can be a default at all, and why EF Core's generics do not break under it.

## What it is worth (measured, Release publish of the standalone client)

Stripping rewrites method bodies in place rather than shrinking files, so it shows up only after
compression:

| | strip on (default) | strip off | delta |
|---|---|---|---|
| managed assemblies, raw | 16,255,787 | 16,255,787 | 0 |
| managed assemblies, Brotli | 3,779,625 | 4,514,789 | **-735,164 (-16.3%)** |
| first-visit Brotli | 15,672,996 | 16,412,790 | -739,794 (-4.5%) |
| `dotnet.native.wasm` raw / Brotli | 59,308,216 / 11,449,979 | 59,308,216 / 11,454,603 | unchanged |

The native module is unaffected: stripping touches managed assemblies only. The largest single
contributors are `System.Private.CoreLib` (-195 KB Brotli), `System.Private.Xml` (-151 KB) and
`System.Data.Common` (-55 KB).

## Why the property is not set

Pinning `true` is dead configuration today, and if a future SDK ever flips the default it will be
because the per-method strip stopped being safe — which is not a default this app should override.
Re-measure with `-p:WasmStripILAfterAOT=false` instead of re-litigating the flag.

The unconditional full strip (`WasmStripAOTAssemblies`) is hardcoded to `false` in
`WasmApp.Common.targets` and cannot be set from the project file.
