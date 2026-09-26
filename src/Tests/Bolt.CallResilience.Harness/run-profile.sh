#!/usr/bin/env bash
# Runs one call through the real relay across a tc-netem shaped Linux link. See README.md.
#
# usage: run-profile.sh NAME IMAGE "RELAY_EGRESS_NETEM" "RECEIVER_EGRESS_NETEM" OUTAGE_AT OUTAGE_SECONDS EXPECT [ENV=VALUE...]
#   RELAY_EGRESS_NETEM     shapes the downlink towards the receiver (the phone), e.g. "delay 500ms 20ms rate 512kbit loss 1%"
#   RECEIVER_EGRESS_NETEM  shapes the receiver's uplink (its ACKs), e.g. "delay 500ms 20ms"
#   OUTAGE_AT              seconds after both participants joined to drop every packet both ways (-1: no outage)
#   OUTAGE_SECONDS         length of that outage
#   EXPECT                 "survive" fails the run unless the call lasted; "any" only records it
#   ENV=VALUE              harness settings passed to the relay (SECONDS, VIDEO_KBPS, FPS, AUDIO_PAYLOAD, KF_MS, ...)
set -euo pipefail
name=$1 image=$2 relaynet=$3 recvnet=$4 outage_at=$5 outage_for=$6 expect=$7
shift 7
envs=()
seconds=180
for setting in "$@"; do
  envs+=(-e "$setting")
  case $setting in SECONDS=*) seconds=${setting#SECONDS=} ;; esac
done

net="callnet-$name" relay="relay-$name" receiver="receiver-$name"
logs=${LOG_DIR:-logs}
mkdir -p "$logs"
cleanup() {
  docker logs "$relay" >"$logs/$name.relay.log" 2>&1 || true
  docker logs "$receiver" >"$logs/$name.receiver.log" 2>&1 || true
  docker rm -f "$relay" "$receiver" >/dev/null 2>&1 || true
  docker network rm "$net" >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker network create "$net" >/dev/null
docker run -d --name "$relay" --network "$net" --network-alias relay --cap-add NET_ADMIN "${envs[@]}" "$image" \
  sh -c "tc qdisc add dev eth0 root netem $relaynet && exec dotnet Bolt.CallResilience.Harness.dll relay" >/dev/null
docker run -d --name "$receiver" --network "$net" --cap-add NET_ADMIN -e "SECONDS=$seconds" "$image" \
  sh -c "tc qdisc add dev eth0 root netem $recvnet && exec dotnet Bolt.CallResilience.Harness.dll receiver" >/dev/null

waitfor() { # container pattern timeout
  local deadline=$((SECONDS + $3))
  until docker logs "$1" 2>&1 | grep -q "$2"; do
    if ((SECONDS > deadline)); then echo "timed out waiting for '$2' in $1" >&2; return 1; fi
    sleep 1
  done
}

waitfor "$relay" "RELAY joined" 120
if ((outage_at >= 0)); then
  sleep "$outage_at"
  docker exec "$relay" tc qdisc change dev eth0 root netem $relaynet loss 100% || true
  docker exec "$receiver" tc qdisc change dev eth0 root netem $recvnet loss 100% || true
  echo "$name: outage for ${outage_for}s at ${outage_at}s"
  sleep "$outage_for"
  docker exec "$relay" tc qdisc change dev eth0 root netem $relaynet || true
  docker exec "$receiver" tc qdisc change dev eth0 root netem $recvnet || true
fi
waitfor "$relay" "^SUMMARY" $((seconds + 60))
timeout 60 docker wait "$receiver" >/dev/null || true
docker logs "$relay" 2>&1 | grep "^SUMMARY" | sed "s/^/$name relay: /"
docker logs "$receiver" 2>&1 | grep "^SUMMARY" | sed "s/^/$name receiver: /" || true

if [ "$expect" = survive ]; then
  if ! docker logs "$relay" 2>&1 | grep "^SUMMARY" | grep -q '"outcome":"survived"'; then
    echo "$name: the call did not survive" >&2
    exit 1
  fi
fi
