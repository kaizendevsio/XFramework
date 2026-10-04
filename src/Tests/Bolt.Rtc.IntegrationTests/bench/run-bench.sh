#!/usr/bin/env bash
# One benchmark scenario: shape the receiver's TURN leg with netem, count every TURN leg with iptables, and run the
# same synthetic call through native WebRTC and Bolt's datagram path (before and after this branch's changes).
#
#   run-bench.sh <scenario> "<downlink netem>" "<uplink netem>" <video kbps> <audio packet ms for bolt-after> <out dir>
#
# The receiver's TURN address is 127.0.0.3: packets from it (TURN to receiver) get the downlink netem (rate, delay,
# loss), packets to it get the uplink netem (delay), so the receiver's round trip to TURN is the scenario's. The sender
# (127.0.0.2) and the Bolt relay's own leg (127.0.0.4) are unshaped. Needs coturn on all three addresses and
# BOLT_RTC_SIDECAR (see call-media-benchmark.yml).
set -euo pipefail
scenario=$1 downlink=$2 uplink=$3 video_kbps=$4 audio_ms=$5 out=$6
mkdir -p "$out"
project=src/Tests/Bolt.Rtc.IntegrationTests/Bolt.Rtc.IntegrationTests.csproj

sudo tc qdisc del dev lo root 2>/dev/null || true
sudo tc qdisc add dev lo root handle 1: prio bands 3 priomap 2 2 2 2 2 2 2 2 2 2 2 2 2 2 2 2
# shellcheck disable=SC2086
sudo tc qdisc add dev lo parent 1:1 handle 10: netem $downlink
# shellcheck disable=SC2086
sudo tc qdisc add dev lo parent 1:2 handle 20: netem $uplink
sudo tc filter add dev lo parent 1: protocol ip prio 1 u32 match ip src 127.0.0.3/32 match ip sport 3478 0xffff flowid 1:1
sudo tc filter add dev lo parent 1: protocol ip prio 2 u32 match ip dst 127.0.0.3/32 match ip dport 3478 0xffff flowid 1:2
trap 'sudo tc qdisc del dev lo root 2>/dev/null || true' EXIT

sudo iptables -N BENCH 2>/dev/null || sudo iptables -F BENCH
sudo iptables -C OUTPUT -o lo -j BENCH 2>/dev/null || sudo iptables -I OUTPUT -o lo -j BENCH
sudo iptables -A BENCH -p udp -d 127.0.0.2 --dport 3478 -m comment --comment sender_up
sudo iptables -A BENCH -p udp -s 127.0.0.2 --sport 3478 -m comment --comment sender_down
sudo iptables -A BENCH -p udp -d 127.0.0.3 --dport 3478 -m comment --comment receiver_up
sudo iptables -A BENCH -p udp -s 127.0.0.3 --sport 3478 -m comment --comment receiver_down
sudo iptables -A BENCH -p udp -d 127.0.0.4 --dport 3478 -m comment --comment relay_up
sudo iptables -A BENCH -p udp -s 127.0.0.4 --sport 3478 -m comment --comment relay_down

for mode in native bolt-before bolt-after; do
    echo "::group::$scenario $mode"
    BENCH_MODE=$mode BENCH_COUNTERS=1 BENCH_VIDEO_KBPS=$video_kbps BENCH_AUDIO_FRAME_MS=$audio_ms \
    BENCH_OUT="$out/$scenario.$mode.json" BOLT_RTC_TURN_SECRET=ci-turn-secret \
        dotnet test "$project" --configuration Release --no-build --filter "FullyQualifiedName~MediaBenchmark" \
        --logger "console;verbosity=normal" || echo "$scenario $mode failed" >>"$out/failures.txt"
    echo "::endgroup::"
done
echo "$scenario|$downlink|$uplink|$video_kbps" >"$out/$scenario.scenario"
