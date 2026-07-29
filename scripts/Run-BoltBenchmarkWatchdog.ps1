[CmdletBinding()]
param(
    [string]$Project = "src/Tests/Bolt.Tests/Bolt.Tests.csproj",
    [string]$AssemblyPath,
    [string[]]$BenchmarkArguments = @("--filter", "*PayloadBenchmarks.GRPC_Echo*"),
    [int]$TimeoutSeconds = 1800,
    [int]$DiagnosticSeconds = 10,
    [string]$Artifacts = "BenchmarkDotNet.Artifacts/watchdog",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$Artifacts = [IO.Path]::GetFullPath($Artifacts)
New-Item -ItemType Directory -Path $Artifacts -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runDirectory = Join-Path $Artifacts $stamp
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

$projectPath = [IO.Path]::GetFullPath($Project)
if (-not $NoBuild) {
    $buildLog = Join-Path $runDirectory "build.log"
    & dotnet build $projectPath -c Release --nologo *> $buildLog
    if ($LASTEXITCODE -ne 0) {
        throw "Benchmark build failed with code $LASTEXITCODE. See $buildLog."
    }
}

if ([string]::IsNullOrWhiteSpace($AssemblyPath)) {
    [xml]$projectXml = Get-Content $projectPath
    $targetFramework = @($projectXml.Project.PropertyGroup.TargetFramework | Where-Object { $_ })[0]
    if ([string]::IsNullOrWhiteSpace($targetFramework)) {
        throw "Could not determine TargetFramework from $projectPath."
    }
    $assemblyName = @($projectXml.Project.PropertyGroup.AssemblyName | Where-Object { $_ })[0]
    if ([string]::IsNullOrWhiteSpace($assemblyName)) {
        $assemblyName = [IO.Path]::GetFileNameWithoutExtension($projectPath)
    }
    $AssemblyPath = Join-Path ([IO.Path]::GetDirectoryName($projectPath)) "bin/Release/$targetFramework/$assemblyName.dll"
}
$AssemblyPath = [IO.Path]::GetFullPath($AssemblyPath)
if (-not (Test-Path $AssemblyPath -PathType Leaf)) {
    throw "Compiled benchmark assembly was not found at $AssemblyPath. Build it first or omit -NoBuild."
}

$stdoutPath = Join-Path $runDirectory "stdout.log"
$stderrPath = Join-Path $runDirectory "stderr.log"
$arguments = @($AssemblyPath) + $BenchmarkArguments
$process = Start-Process dotnet -ArgumentList $arguments -PassThru -NoNewWindow `
    -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
$null = $process.Handle

function Get-ProcessTreeIds([int]$RootProcessId) {
    $ids = [Collections.Generic.HashSet[int]]::new()
    [void]$ids.Add($RootProcessId)

    if ($IsWindows -or $env:OS -eq "Windows_NT") {
        $rows = Get-CimInstance Win32_Process | ForEach-Object {
            [pscustomobject]@{ Id = [int]$_.ProcessId; ParentId = [int]$_.ParentProcessId }
        }
    }
    else {
        $rows = & ps -eo pid=,ppid= | ForEach-Object {
            if ($_ -match '^\s*(\d+)\s+(\d+)\s*$') {
                [pscustomobject]@{ Id = [int]$Matches[1]; ParentId = [int]$Matches[2] }
            }
        }
    }

    do {
        $added = $false
        foreach ($row in $rows) {
            if ($ids.Contains($row.ParentId) -and -not $ids.Contains($row.Id)) {
                $added = $ids.Add($row.Id) -or $added
            }
        }
    } while ($added)
    return @($ids)
}

function Get-DiagnosticProcesses([int]$RootProcessId) {
    return Get-ProcessTreeIds $RootProcessId | ForEach-Object {
        Get-Process -Id $_ -ErrorAction SilentlyContinue
    }
}

function Invoke-DiagnosticTool(
    [string]$Tool,
    [string[]]$Arguments,
    [string]$LogPath,
    [string]$ExpectedOutputPath,
    [int]$MaximumSeconds) {
    $available = [bool](Get-Command $Tool -ErrorAction SilentlyContinue)
    if (-not $available) {
        return [ordered]@{ available = $false; captured = $false; exitCode = $null; output = $ExpectedOutputPath }
    }

    try {
        $capture = Start-Process $Tool -ArgumentList $Arguments -PassThru -NoNewWindow `
            -RedirectStandardOutput $LogPath -RedirectStandardError "$LogPath.err"
        $null = $capture.Handle
        $completed = $capture.WaitForExit($MaximumSeconds * 1000)
        if ($completed) {
            $capture.WaitForExit()
            $capture.Refresh()
        }
        elseif (-not $capture.HasExited) {
            Stop-Process -Id $capture.Id -Force -ErrorAction SilentlyContinue
        }
        $exitCode = if ($completed) { $capture.ExitCode } else { $null }
        $captured = $completed -and $exitCode -eq 0 -and
            (Test-Path $ExpectedOutputPath -PathType Leaf) -and
            (Get-Item $ExpectedOutputPath).Length -gt 0
        return [ordered]@{
            available = $true
            captured = $captured
            exitCode = $exitCode
            output = $ExpectedOutputPath
        }
    }
    catch {
        $_.Exception.ToString() | Set-Content "$LogPath.start-error"
        return [ordered]@{ available = $true; captured = $false; exitCode = $null; output = $ExpectedOutputPath }
    }
}

$completed = $process.WaitForExit($TimeoutSeconds * 1000)
if ($completed) {
    $process.WaitForExit()
    $process.Refresh()
}
$diagnostics = @()

if (-not $completed) {
    $duration = [TimeSpan]::FromSeconds($DiagnosticSeconds).ToString('c')
    $diagnosticProcesses = @(Get-DiagnosticProcesses $process.Id)
    foreach ($target in $diagnosticProcesses) {
        $prefix = Join-Path $runDirectory "pid-$($target.Id)"
        $counterOutput = "$prefix-counters.json"
        $traceOutput = "$prefix-trace.nettrace"
        $dumpOutput = "$prefix.dmp"
        $counters = Invoke-DiagnosticTool "dotnet-counters" @(
            "collect", "--process-id", "$($target.Id)",
            "--duration", $duration, "--format", "json", "--output", $counterOutput
        ) "$prefix-counters.log" $counterOutput ($DiagnosticSeconds + 10)
        $trace = Invoke-DiagnosticTool "dotnet-trace" @(
            "collect", "--process-id", "$($target.Id)",
            "--duration", $duration, "--output", $traceOutput
        ) "$prefix-trace.log" $traceOutput ($DiagnosticSeconds + 10)
        $dump = Invoke-DiagnosticTool "dotnet-dump" @(
            "collect", "--process-id", "$($target.Id)", "--output", $dumpOutput
        ) "$prefix-dump.log" $dumpOutput 60

        $diagnostics += [ordered]@{
            processId = $target.Id
            processName = $target.ProcessName
            counters = $counters
            trace = $trace
            dump = $dump
        }
    }

    $treeIds = @(Get-ProcessTreeIds $process.Id)
    [array]::Reverse($treeIds)
    foreach ($processId in $treeIds) {
        if ($processId -eq $PID) { continue }
        Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
    }
}

$manifest = [ordered]@{
    startedAt = $stamp
    outputDirectory = $runDirectory
    completed = $completed
    timedOut = -not $completed
    timeoutSeconds = $TimeoutSeconds
    diagnosticSeconds = $DiagnosticSeconds
    processId = $process.Id
    exitCode = if ($completed) { $process.ExitCode } else { $null }
    project = $projectPath
    assembly = $AssemblyPath
    arguments = $BenchmarkArguments
    effectiveEnvironment = [ordered]@{}
    diagnostics = $diagnostics
}
Get-ChildItem Env: | Where-Object {
    $_.Name.StartsWith("BOLT_BENCH_", [StringComparison]::Ordinal) -or
    $_.Name.StartsWith("DOTNET_", [StringComparison]::Ordinal) -or
    $_.Name.StartsWith("COMPlus_", [StringComparison]::Ordinal)
} | Sort-Object Name | ForEach-Object {
    $manifest.effectiveEnvironment[$_.Name] = $_.Value
}
$manifestPath = Join-Path $runDirectory "watchdog-manifest.json"
$manifest | ConvertTo-Json -Depth 10 | Set-Content $manifestPath

Write-Host "Watchdog artifacts: $runDirectory"
if (-not $completed) { throw "Benchmark exceeded ${TimeoutSeconds}s; diagnostics were captured before termination." }
if ($process.ExitCode -ne 0) { throw "Benchmark exited with code $($process.ExitCode). See $stderrPath." }
