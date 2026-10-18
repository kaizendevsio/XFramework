#!/usr/bin/env bash
set -euo pipefail
scenario=$1 profile=$2 clean_video=$3 out=$4
case "$scenario" in
  clean-20mbit) downlink="delay 10ms rate 20mbit"; uplink="delay 10ms"; video=$clean_video ;;
  512k-500ms-1pct) downlink="delay 250ms 10ms rate 512kbit loss 1%"; uplink="delay 250ms 10ms"; video=300 ;;
  512k-1000ms-3pct) downlink="delay 500ms 20ms rate 512kbit loss 3%"; uplink="delay 500ms 20ms"; video=300 ;;
  *) echo "Unknown scenario: $scenario" >&2; exit 2 ;;
esac
bash src/Tests/Bolt.Rtc.IntegrationTests/bench/run-bench.sh "$scenario-$profile" "$downlink" "$uplink" "$video" "$out"
