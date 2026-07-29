[CmdletBinding()]
param(
    [string]$Project = "src/Tests/Bolt.Tests/Bolt.Tests.csproj",
    [string[]]$BenchmarkArguments = @("--filter", "*PayloadBenchmarks*"),
    [string[]]$BaselineEnvironment = @(
        "BOLT_BENCH_STREAM_CHUNK_SIZE_BYTES=262144",
        "BOLT_BENCH_HUB_RECEIVE_BUFFER_BYTES=262144"
    ),
    [string[]]$CandidateEnvironment = @(
        "BOLT_BENCH_STREAM_CHUNK_SIZE_BYTES=262123",
        "BOLT_BENCH_HUB_RECEIVE_BUFFER_BYTES=262144"
    ),
    [switch]$ChunkSweep,
    [int[]]$ChunkSweepBytes = @(65515, 131051, 262123, 262144),
    [int]$Repetitions = 3,
    [int]$Seed = 73421,
    [int]$TimeoutSeconds = 1800,
    [string]$Artifacts = "BenchmarkDotNet.Artifacts/blocks",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
if ($Repetitions -lt 1) { throw "Repetitions must be positive." }
if ($BenchmarkArguments -contains "--artifacts") {
    throw "Do not pass --artifacts in BenchmarkArguments; the block runner assigns a unique directory per block."
}

function Convert-Environment([string[]]$Entries) {
    $settings = [ordered]@{}
    foreach ($entry in $Entries) {
        $parts = $entry.Split('=', 2)
        if ($parts.Count -ne 2 -or [string]::IsNullOrWhiteSpace($parts[0])) {
            throw "Environment setting '$entry' must use NAME=VALUE syntax."
        }
        $settings[$parts[0]] = $parts[1]
    }
    return $settings
}

$baselineSettings = Convert-Environment $BaselineEnvironment
if ($ChunkSweep) {
    $variants = @($ChunkSweepBytes | ForEach-Object {
        $settings = [ordered]@{}
        foreach ($name in $baselineSettings.Keys) { $settings[$name] = $baselineSettings[$name] }
        $settings["BOLT_BENCH_STREAM_CHUNK_SIZE_BYTES"] = $_.ToString()
        [ordered]@{ label = "chunk-$_"; settings = $settings }
    })
}
else {
    $variants = @(
        [ordered]@{ label = "baseline"; settings = $baselineSettings },
        [ordered]@{ label = "candidate"; settings = Convert-Environment $CandidateEnvironment }
    )
}

$random = [Random]::new($Seed)
$blocks = @()
for ($repetition = 1; $repetition -le $Repetitions; $repetition++) {
    $order = @($variants)
    for ($index = $order.Count - 1; $index -gt 0; $index--) {
        $swap = $random.Next($index + 1)
        $temporary = $order[$index]
        $order[$index] = $order[$swap]
        $order[$swap] = $temporary
    }
    for ($position = 0; $position -lt $order.Count; $position++) {
        $blocks += [ordered]@{
            repetition = $repetition
            position = $position + 1
            label = $order[$position].label
            settings = $order[$position].settings
        }
    }
}

$Artifacts = [IO.Path]::GetFullPath($Artifacts)
New-Item -ItemType Directory -Path $Artifacts -Force | Out-Null
$runDirectory = Join-Path $Artifacts (Get-Date -Format "yyyyMMdd-HHmmss")
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

if (-not $NoBuild) {
    $buildLog = Join-Path $runDirectory "build.log"
    & dotnet build $Project -c Release --nologo *> $buildLog
    if ($LASTEXITCODE -ne 0) { throw "Benchmark build failed. See $buildLog." }
}

$manifest = [ordered]@{
    generatedAt = [DateTimeOffset]::UtcNow
    mode = if ($ChunkSweep) { "chunk-sweep" } else { "baseline-candidate" }
    seed = $Seed
    repetitions = $Repetitions
    project = $Project
    benchmarkArguments = $BenchmarkArguments
    chunkSweepBytes = if ($ChunkSweep) { $ChunkSweepBytes } else { $null }
    blocks = $blocks
    results = @()
}
$manifestPath = Join-Path $runDirectory "block-manifest.json"
$manifest | ConvertTo-Json -Depth 10 | Set-Content $manifestPath

foreach ($block in $blocks) {
    $saved = @{}
    $savedArtifacts = [Environment]::GetEnvironmentVariable("BOLT_BENCH_ARTIFACTS", "Process")
    $blockDirectory = Join-Path $runDirectory "r$($block.repetition)-p$($block.position)-$($block.label)"
    $bdnDirectory = Join-Path $blockDirectory "benchmarkdotnet"
    $watchdogRoot = Join-Path $blockDirectory "watchdog"
    try {
        foreach ($name in $block.settings.Keys) {
            $saved[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
            [Environment]::SetEnvironmentVariable($name, $block.settings[$name], "Process")
        }
        [Environment]::SetEnvironmentVariable(
            "BOLT_BENCH_ARTIFACTS",
            (Join-Path $blockDirectory "effective-settings"),
            "Process")

        $blockArguments = @($BenchmarkArguments) + @("--artifacts", $bdnDirectory)
        & (Join-Path $PSScriptRoot "Run-BoltBenchmarkWatchdog.ps1") `
            -Project $Project `
            -BenchmarkArguments $blockArguments `
            -TimeoutSeconds $TimeoutSeconds `
            -Artifacts $watchdogRoot `
            -NoBuild

        $watchdogManifest = Get-ChildItem $watchdogRoot -Recurse -Filter watchdog-manifest.json |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if (-not $watchdogManifest) { throw "Watchdog manifest was not written for $($block.label)." }
        $watchdogData = Get-Content $watchdogManifest.FullName -Raw | ConvertFrom-Json
        $reports = @(Get-ChildItem $bdnDirectory -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Extension -in '.csv', '.md', '.html', '.log' } |
            Select-Object -ExpandProperty FullName)
        if ($reports.Count -eq 0) { throw "BenchmarkDotNet wrote no CSV, Markdown, HTML, or log artifacts." }

        $manifest.results += [ordered]@{
            repetition = $block.repetition
            position = $block.position
            label = $block.label
            status = "completed"
            blockDirectory = $blockDirectory
            benchmarkDotNetDirectory = $bdnDirectory
            watchdogOutputDirectory = $watchdogData.outputDirectory
            watchdogManifest = $watchdogManifest.FullName
            reports = $reports
        }
    }
    catch {
        $manifest.results += [ordered]@{
            repetition = $block.repetition
            position = $block.position
            label = $block.label
            status = "failed"
            blockDirectory = $blockDirectory
            error = $_.Exception.Message
        }
        throw
    }
    finally {
        foreach ($name in $block.settings.Keys) {
            [Environment]::SetEnvironmentVariable($name, $saved[$name], "Process")
        }
        [Environment]::SetEnvironmentVariable("BOLT_BENCH_ARTIFACTS", $savedArtifacts, "Process")
        $manifest | ConvertTo-Json -Depth 10 | Set-Content $manifestPath
    }
}

Write-Host "Randomized block artifacts: $runDirectory"
