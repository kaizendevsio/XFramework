# Shapes each leg of a browser call on loopback, for call-browser-e2e.yml. Source it, then call shape_call and (on exit)
# unshape_call. Every argument is a netem specification ("delay 7ms rate 50mbit loss 1%"), or "none".
#
#   shape_call "<A down>" "<A up>" "<B down>" "<B up>" "<relay>"
#
# A's leg is its TURN address 127.0.0.2:3478 (down: TURN to the browser, up: the browser to TURN), B's 127.0.0.3:3478, and
# the relay's own leg 127.0.0.4:3478 (both directions). The hop between TURN allocations and the test's own HTTPS stay
# untouched.

shape_call() {
  local specs=("$1" "$2" "$3" "$4" "$5")
  sudo tc qdisc del dev lo root 2>/dev/null || true
  # Bands 1:1-1:5 are the shaped legs; everything else maps to 1:6, unshaped.
  sudo tc qdisc add dev lo root handle 1: prio bands 6 priomap 5 5 5 5 5 5 5 5 5 5 5 5 5 5 5 5
  local band
  for band in 1 2 3 4 5; do
    local spec=${specs[$((band - 1))]}
    if [ "$spec" != "none" ]; then
      # A deep queue unless the scenario sizes it (a link's buffer decides whether overload shows as delay or loss).
      case "$spec" in *limit*) ;; *) spec="$spec limit 10000" ;; esac
      # shellcheck disable=SC2086
      sudo tc qdisc add dev lo parent "1:$band" handle "${band}0:" netem $spec
    fi
  done
  sudo tc qdisc add dev lo parent 1:6 handle 60: pfifo_fast 2>/dev/null || true
  sudo tc filter add dev lo parent 1: protocol ip prio 1 u32 match ip src 127.0.0.2/32 match ip sport 3478 0xffff flowid 1:1
  sudo tc filter add dev lo parent 1: protocol ip prio 1 u32 match ip dst 127.0.0.2/32 match ip dport 3478 0xffff flowid 1:2
  sudo tc filter add dev lo parent 1: protocol ip prio 1 u32 match ip src 127.0.0.3/32 match ip sport 3478 0xffff flowid 1:3
  sudo tc filter add dev lo parent 1: protocol ip prio 1 u32 match ip dst 127.0.0.3/32 match ip dport 3478 0xffff flowid 1:4
  sudo tc filter add dev lo parent 1: protocol ip prio 1 u32 match ip src 127.0.0.4/32 match ip sport 3478 0xffff flowid 1:5
  sudo tc filter add dev lo parent 1: protocol ip prio 1 u32 match ip dst 127.0.0.4/32 match ip dport 3478 0xffff flowid 1:5
  sudo tc -s qdisc show dev lo | head -40
}

# Change one leg mid-call (a step down): band 1 A down, 2 A up, 3 B down, 4 B up, 5 the relay's leg.
reshape_leg() {
  local band=$1 spec=$2
  case "$spec" in *limit*) ;; *) spec="$spec limit 10000" ;; esac
  # shellcheck disable=SC2086
  sudo tc qdisc change dev lo parent "1:$band" handle "${band}0:" netem $spec
  echo "reshaped band $band: $spec"
}

unshape_call() {
  sudo tc -s qdisc show dev lo 2>/dev/null | head -60 || true
  sudo tc qdisc del dev lo root 2>/dev/null || true
}
