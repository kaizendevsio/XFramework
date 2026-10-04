# Shapes the two TURN legs of a call on loopback, for call-udp-integration.yml. Source it, then call shape_legs and
# (on exit) unshape_legs.
#   browser's leg (to/from port 3478): 40 ms each way, 1% loss, 4 Mbit/s
#   relay's leg (to/from ports 3480 and 5350, its own TURN server over UDP and TLS): 8 ms each way, 8 Mbit/s
# Everything else on loopback (the hop between the two TURN servers, the test's own HTTP) is untouched.

shape_legs() {
  sudo tc qdisc add dev lo root handle 1: prio bands 5 priomap 1 2 2 2 1 2 0 0 1 1 1 1 1 1 1 1
  sudo tc qdisc add dev lo parent 1:4 handle 40: netem delay 40ms loss 1% rate 4mbit limit 4000
  sudo tc qdisc add dev lo parent 1:5 handle 50: netem delay 8ms rate 8mbit limit 4000
  for port in 3478; do
    sudo tc filter add dev lo parent 1: protocol ip prio 1 u32 match ip dport "$port" 0xffff flowid 1:4
    sudo tc filter add dev lo parent 1: protocol ip prio 1 u32 match ip sport "$port" 0xffff flowid 1:4
  done
  for port in 3480 5350; do
    sudo tc filter add dev lo parent 1: protocol ip prio 1 u32 match ip dport "$port" 0xffff flowid 1:5
    sudo tc filter add dev lo parent 1: protocol ip prio 1 u32 match ip sport "$port" 0xffff flowid 1:5
  done
}

unshape_legs() {
  sudo tc qdisc del dev lo root 2>/dev/null || true
}
