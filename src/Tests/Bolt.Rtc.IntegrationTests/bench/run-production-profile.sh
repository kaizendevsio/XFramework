#!/usr/bin/env bash
# Production WASM media/pacer/relay, synthetic encoded payloads: never merge with the real-codec/native table.
set -euo pipefail
snapshot=$1 out=$2
mkdir -p "$out"
source .github/scripts/call-shape-browsers.sh
trap 'unshape_call' EXIT
case "$CALL_SCENARIO" in
  clean-1080p30) a_down="delay 10ms rate 20mbit"; a_up="delay 10ms" ;;
  512k-1000ms-3pct) a_down="delay 500ms 20ms rate 512kbit loss 3%"; a_up="delay 500ms 20ms" ;;
  *) echo "Unknown production scenario: $CALL_SCENARIO" >&2; exit 2 ;;
esac
# Existing correctness assertions remain visible in JSON. A severe-profile quality failure is a measurement,
# not a reason to hide its results; setup failures still fail this script.
git -C measured rev-parse HEAD >"$out/$CALL_SCENARIO.$snapshot.revision"
for repeat in $(seq 1 "${BENCH_REPEATS:-1}"); do
  shape_call "$a_down" "$a_up" "delay 10ms rate 20mbit" "delay 10ms" "delay 3ms"
  export CALL_OUT="$out/$CALL_SCENARIO.$snapshot.r$repeat.production.json"
  if ! dotnet test measured/src/Tests/Bolt.Rtc.IntegrationTests/Bolt.Rtc.IntegrationTests.csproj --configuration Release --no-build \
    --filter 'TestCategory=BrowserCall' --logger 'console;verbosity=normal'; then
    if [ ! -f "$CALL_OUT" ]; then exit 1; fi
    echo "$CALL_SCENARIO $snapshot r$repeat correctness assertions failed; see JSON" >>"$out/quality-failures.txt"
  fi
  unshape_call
done
