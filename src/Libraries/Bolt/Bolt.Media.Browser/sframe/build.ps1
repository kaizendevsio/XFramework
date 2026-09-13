param([string]$ClangDirectory)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    if ($ClangDirectory) {
        $env:CC_wasm32_unknown_unknown = Join-Path $ClangDirectory 'clang.exe'
        $env:AR_wasm32_unknown_unknown = Join-Path $ClangDirectory 'llvm-ar.exe'
    }
    if ((& wasm-bindgen --version) -ne 'wasm-bindgen 0.2.100') {
        throw 'Install pinned generator: cargo install wasm-bindgen-cli --version 0.2.100 --locked'
    }
    $env:CARGO_TARGET_WASM32_UNKNOWN_UNKNOWN_RUNNER = 'wasm-bindgen-test-runner'
    & cargo test --locked --target wasm32-unknown-unknown
    if ($LASTEXITCODE) { throw 'SFrame WASM tests failed' }
    & cargo build --locked --release --target wasm32-unknown-unknown
    if ($LASTEXITCODE) { throw 'SFrame WASM build failed' }
    & wasm-bindgen target/wasm32-unknown-unknown/release/bolt_sframe.wasm --target web --out-dir ../wwwroot/sframe --out-name bolt_sframe --no-typescript
    if ($LASTEXITCODE) { throw 'SFrame bindings failed' }
    & node --test ../test/sframe.test.mjs
    if ($LASTEXITCODE) { throw 'SFrame adapter tests failed' }
    $assets = @('bolt_sframe.js', 'bolt_sframe_bg.wasm') | ForEach-Object {
        $asset = Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path '../wwwroot/sframe' $_)
        "$($asset.Hash.ToLowerInvariant())  $_"
    }
    Set-Content -LiteralPath '../wwwroot/sframe/SHA256SUMS' -Value $assets -Encoding ascii
    $metadata = & cargo metadata --locked --format-version 1 | ConvertFrom-Json
    if ($LASTEXITCODE) { throw 'Dependency license inventory failed' }
    $licenses = [System.Text.StringBuilder]::new()
    foreach ($package in $metadata.packages | Where-Object source | Sort-Object name) {
        [void]$licenses.AppendLine("===== $($package.name) $($package.version) ($($package.license)) =====")
        foreach ($file in Get-ChildItem -LiteralPath (Split-Path $package.manifest_path) -File |
            Where-Object { $_.Name -match '^(LICENSE|COPYING|NOTICE)' } | Sort-Object Name) {
            [void]$licenses.AppendLine("--- $($file.Name) ---")
            [void]$licenses.AppendLine([IO.File]::ReadAllText($file.FullName))
        }
    }
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot '../wwwroot/sframe/THIRD-PARTY-LICENSES.txt'), $licenses.ToString())
} finally { Pop-Location }
