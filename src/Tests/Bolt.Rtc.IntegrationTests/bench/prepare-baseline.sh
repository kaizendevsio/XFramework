#!/usr/bin/env bash
# Share only measurement code. Production modules, browser assets and sidecar remain the baseline snapshot's own.
set -euo pipefail
snapshot=${1:?baseline checkout directory required}
source_dir=src/Tests/Bolt.Rtc.IntegrationTests
target_dir="$snapshot/$source_dir"
test -f "$target_dir/Bolt.Rtc.IntegrationTests.csproj"
cp "$source_dir/MediaBenchmark.cs" "$source_dir/RecoveryBufferBenchmarkTests.cs" "$source_dir/Bolt.Rtc.IntegrationTests.csproj" "$target_dir/"
cp -R "$source_dir/bench/." "$target_dir/bench/"
