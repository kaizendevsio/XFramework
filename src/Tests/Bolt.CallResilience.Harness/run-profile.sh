#!/usr/bin/env bash
# Runs one call through the real relay across a tc-netem shaped Linux link. See README.md.
#
# usage: run-profile.sh NAME IMAGE "RELAY_EGRESS_NETEM" "RECEIVER_EGRESS_NETEM" OUTAGE_AT OUTAGE_SECONDS EXPECT [ENV=VALUE...]
#   RELAY_EGRESS_NETEM     shapes the downlink towards the receiver (the phone), e.g. "delay 500ms 20ms rate 512kbit loss 1%"
#   RECEIVER_EGRESS_NETEM  shapes the receiver's uplink (its ACKs), e.g. "delay 500ms 20ms"
#   OUTAGE_AT              seconds after both participants joined to drop every packet both ways (-1: no outage)
#   OUTAGE_SECONDS         length of that outage
#   EXPECT                 "survive" fails the run unless the call lasted; "any" only records it;
#                          "resume" (RESUME=1) needs the call to last and the receiver to have resumed;
#                          "resume-retired" also needs the relay to have retired the receiver first;
#                          "end-clean" needs the held seat to expire (after the grace) and the receiver to give up;
#                          "video-climbs" (ADAPTIVE=1) needs the call to last with video never suspended and settling at
#                          or above MIN_VIDEO_KBPS (default 1000); "audio-continues" needs the call to last with no
#                          audio gap longer than MAX_AUDIO_GAP_MS (default 6000)
#   ENV=VALUE              harness settings passed to the relay (SECONDS, VIDEO_KBPS, FPS, AUDIO_PAYLOAD, KF_MS, ...)
#                          UDP=1 adds a TURN server (coturn) to the run's network and lets the receiver move its
#                          media onto a WebRTC data channel through it; UDP_BLOCK=1 also drops the receiver's UDP
#                          to that server, as on a network that blocks UDP (the call must stay on its WebSocket).
#
# NETEM_STEPS (environment, optional) changes the downlink mid-call, e.g. a bandwidth step down and back up:
#   NETEM_STEPS="60=delay 50ms 10ms rate 512kbit|120=delay 50ms 10ms rate 4mbit"
set -euo pipefail
name=$1 image=$2 relaynet=$3 recvnet=$4 outage_at=$5 outage_for=$6 expect=$7
shift 7
envs=()
seconds=180
udp=0 udp_block=0
for setting in "$@"; do
  envs+=(-e "$setting")
  case $setting in
    SECONDS=*) seconds=${setting#SECONDS=} ;;
    UDP=1) udp=1 ;;
    UDP_BLOCK=1) udp_block=1 ;;
  esac
done

net="callnet-$name" relay="relay-$name" receiver="receiver-$name" turn="turn-$name"
turn_image=${TURN_IMAGE:-coturn/coturn:4.18.0-debian@sha256:bbefd3e1fdfdc0d58770fe01b581fd8b00d9f3a5580d00acb77cf719a6bc78e3}
logs=${LOG_DIR:-logs}
mkdir -p "$logs"
cleanup() {
  docker logs "$relay" >"$logs/$name.relay.log" 2>&1 || true
  docker logs "$receiver" >"$logs/$name.receiver.log" 2>&1 || true
  if [ "$udp" = 1 ]; then docker logs "$turn" >"$logs/$name.turn.log" 2>&1 || true; fi
  docker rm -f "$relay" "$receiver" "$turn" >/dev/null 2>&1 || true
  docker network rm "$net" >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker network create "$net" >/dev/null
receiver_prefix=""
if [ "$udp" = 1 ]; then
  # TURN for both sides, as Cloudflare is for Yap: the relay allocates over UDP (relay-only), the receiver may use any
  # candidate to reach it. Credentials are TURN REST ones derived from a shared secret, short-lived like Cloudflare's.
  docker run -d --name "$turn" --network "$net" --network-alias turn "$turn_image" \
    -n --log-file=stdout --listening-port=3478 --use-auth-secret --static-auth-secret=harness-turn-secret \
    --realm=harness --fingerprint --no-tls --min-port=49160 --max-port=49760 >/dev/null
  envs+=(-e "TURN_URL=turn:turn:3478?transport=udp" -e "TURN_SECRET=harness-turn-secret")
  if [ "$udp_block" = 1 ]; then
    receiver_prefix='iptables -I OUTPUT -p udp -d "$(getent hosts turn | cut -d" " -f1)" -j DROP && '
  fi
fi
docker run -d --name "$relay" --network "$net" --network-alias relay --cap-add NET_ADMIN "${envs[@]}" "$image" \
  sh -c "tc qdisc add dev eth0 root netem $relaynet && exec dotnet Bolt.CallResilience.Harness.dll relay" >/dev/null
docker run -d --name "$receiver" --network "$net" --cap-add NET_ADMIN "${envs[@]}" -e "SECONDS=$seconds" "$image" \
  sh -c "tc qdisc add dev eth0 root netem $recvnet && ${receiver_prefix}exec dotnet Bolt.CallResilience.Harness.dll receiver" >/dev/null

waitfor() { # container pattern timeout
  local deadline=$((SECONDS + $3))
  until docker logs "$1" 2>&1 | grep -q "$2"; do
    if ((SECONDS > deadline)); then echo "timed out waiting for '$2' in $1" >&2; return 1; fi
    sleep 1
  done
}

waitfor "$relay" "RELAY joined" 120
steps_pid=
if [ -n "${NETEM_STEPS:-}" ]; then
  (
    started=$SECONDS
    IFS='|' read -r -a steps <<<"$NETEM_STEPS"
    for step in "${steps[@]}"; do
      at=${step%%=*} shape=${step#*=}
      while ((SECONDS - started < at)); do sleep 1; done
      docker exec "$relay" tc qdisc change dev eth0 root netem $shape || true
      echo "$name: downlink is now '$shape' at ${at}s"
    done
  ) &
  steps_pid=$!
fi
if ((outage_at >= 0)); then
  sleep "$outage_at"
  docker exec "$relay" tc qdisc change dev eth0 root netem $relaynet loss 100% || true
  docker exec "$receiver" tc qdisc change dev eth0 root netem $recvnet loss 100% || true
  # The containers share the host clock, so the receiver can time its recovery from these.
  docker exec "$receiver" sh -c 'date +%s%3N > /tmp/outage-start' || true
  echo "$name: outage for ${outage_for}s at ${outage_at}s"
  sleep "$outage_for"
  docker exec "$relay" tc qdisc change dev eth0 root netem $relaynet || true
  docker exec "$receiver" tc qdisc change dev eth0 root netem $recvnet || true
  docker exec "$receiver" sh -c 'date +%s%3N > /tmp/outage-end' || true
fi
waitfor "$relay" "^SUMMARY" $((seconds + 60))
if [ -n "$steps_pid" ]; then wait "$steps_pid" || true; fi
timeout 60 docker wait "$receiver" >/dev/null || true
docker logs "$relay" 2>&1 | grep "^SUMMARY" | sed "s/^/$name relay: /"
docker logs "$receiver" 2>&1 | grep "^SUMMARY" | sed "s/^/$name receiver: /" || true

relay_summary=$(docker logs "$relay" 2>&1 | grep "^SUMMARY" || true)
receiver_summary=$(docker logs "$receiver" 2>&1 | grep "^SUMMARY" || true)
case $expect in
  survive | resume | resume-retired | video-climbs | audio-continues)
    if ! grep -q '"outcome":"survived"' <<<"$relay_summary"; then
      echo "$name: the call did not survive" >&2
      exit 1
    fi ;;
esac
case $expect in
  resume | resume-retired)
    if ! grep -q '"gaveUp":false' <<<"$receiver_summary" || grep -q '"resumes":\[\]' <<<"$receiver_summary"; then
      echo "$name: the receiver did not resume" >&2
      exit 1
    fi ;;
esac
# Read the log once: with pipefail, `docker logs | grep -q` fails whenever grep stops reading early.
relay_log=$(docker logs "$relay" 2>&1 || true)
if [ "$expect" = resume-retired ] && ! grep -q "Retiring Bolt connection" <<<"$relay_log"; then
  echo "$name: the relay never retired the stalled receiver" >&2
  exit 1
fi
summary_field() { # json-line field.path
  python3 -c 'import json,sys
value = json.loads(sys.argv[1].split(" ", 1)[1])
for key in sys.argv[2].split("."):
    value = (value or {}).get(key) if isinstance(value, dict) else None
print("" if value is None else value)' "$1" "$2"
}
if [ "$expect" = video-climbs ]; then
  suspended=$(summary_field "$relay_summary" rate.suspendedSeconds)
  settled=$(summary_field "$relay_summary" rate.settledVideoKbpsMedian)
  echo "$name: video suspended ${suspended}s, settled at ${settled} kbps"
  if [ "${suspended%.*}" != 0 ] || [ "${settled:-0}" -lt "${MIN_VIDEO_KBPS:-1000}" ]; then
    echo "$name: video did not stay on and climb on a fast path" >&2
    exit 1
  fi
fi
if [ "$expect" = audio-continues ]; then
  gap=$(summary_field "$receiver_summary" longestAudioGapMs)
  echo "$name: longest audio gap ${gap} ms"
  if [ "${gap:-999999}" -gt "${MAX_AUDIO_GAP_MS:-6000}" ]; then
    echo "$name: audio stopped for ${gap} ms while the WebSocket was fine" >&2
    exit 1
  fi
fi
if [ "$expect" = end-clean ]; then
  if ! grep -q 'seat expired' <<<"$relay_summary" || ! grep -q '"gaveUp":true' <<<"$receiver_summary"; then
    echo "$name: the call did not end cleanly after the grace period" >&2
    exit 1
  fi
fi
